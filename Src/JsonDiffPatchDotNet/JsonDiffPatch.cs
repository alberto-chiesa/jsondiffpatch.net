using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace JsonDiffPatchDotNet
{
	public sealed class Options
	{
		public Options()
		{
			ArrayDiff = ArrayDiffMode.Efficient;
		}

		/// <summary>
		/// Specifies how arrays are diffed. The default is Efficient.
		/// </summary>
		public ArrayDiffMode ArrayDiff { get; set; }

		/// <summary>
		/// Specifies which paths to exclude from the diff patch set
		/// </summary>
		public List<string> ExcludePaths { get; set; } = new List<string>();

		/// <summary>
		/// Specifies behaviors to apply to the diff patch set
		/// </summary>
		public DiffBehavior DiffBehaviors { get; set; }

		public bool IgnoreMissingProperties => DiffBehaviors.HasFlag(DiffBehavior.IgnoreMissingProperties);
		public bool IgnoreNewProperties => DiffBehaviors.HasFlag(DiffBehavior.IgnoreNewProperties);
	}

	public class JsonDiffPatch
	{
		private readonly Options _options;

		public JsonDiffPatch() : this(new Options())
		{
		}

		public JsonDiffPatch(Options options)
		{
			if (options == null) throw new ArgumentNullException(nameof(options));

			_options = options;
		}

		private HashSet<string> _exclusions = new HashSet<string>();

		private void RefreshExclusions()
			=> _exclusions = new HashSet<string>(_options.ExcludePaths ?? Enumerable.Empty<string>(),
				StringComparer.OrdinalIgnoreCase);

		private bool IsPathExcluded(string path) => path != null && _exclusions.Contains(path);

		/// <summary>
		/// Diff two JSON objects.
		/// 
		/// The output is a JObject that contains enough information to represent the
		/// delta between the two objects and to be able perform patch and reverse operations.
		/// </summary>
		/// <param name="left">The base JSON object</param>
		/// <param name="right">The JSON object to compare against the base</param>
		/// <returns>JSON Patch Document</returns>
		public JToken Diff(JToken left, JToken right)
		{
			left ??= new JValue("");
			right ??= new JValue("");

			if (left.Type == JTokenType.Object && right.Type == JTokenType.Object)
				return ObjectDiff((JObject) left, (JObject) right);

			if (_options.ArrayDiff == ArrayDiffMode.Efficient
			    && left.Type == JTokenType.Array
			    && right.Type == JTokenType.Array)
				return ArrayDiff((JArray) left, (JArray) right);

			return JToken.DeepEquals(left, right) ? null : new JArray(left, right);
		}

		/// <summary>
		/// Patch a JSON object
		/// </summary>
		/// <param name="left">Unpatched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Patched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public JToken Patch(JToken left, JToken patch)
			=> patch?.Type switch
			{
				null => left,
				JTokenType.Object => PatchWithObject(left, (JObject) patch),
				JTokenType.Array => ApplyDiff((JArray) patch),
				_ => null
			};

		private JToken PatchWithObject(JToken left, JObject patchObj)
			=> left?.Type == JTokenType.Array && IsArrayPatch(patchObj)
				? (JToken) ArrayPatch((JArray) left, patchObj)
				: ObjectPatch(left as JObject, patchObj);

		private static JToken ApplyDiff(JArray patchArray)
		{
			switch (patchArray.Count)
			{
				// Add
				case 1:
					return patchArray[0];
				// Replace
				case 2:
					return patchArray[1];
				// Delete, Move or TextDiff
				case 3:
				{
					if (patchArray[2].Type != JTokenType.Integer)
						throw new InvalidDataException("Invalid patch object");

					return patchArray[2].Value<int>() switch
					{
						(int) DiffOperation.Deleted => null,
						// TODO: is array move not necessary?
						// (int) DiffOperation.ArrayMove => throw new InvalidDataException("Invalid patch object"),
						_ => throw new InvalidDataException("Invalid patch object")
					};
				}
				default:
					throw new InvalidDataException("Invalid patch object");
			}
		}

		/// <summary>
		/// Unpatch a JSON object
		/// </summary>
		/// <param name="right">Patched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Unpatched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public JToken Unpatch(JToken right, JToken patch)
			=> patch?.Type switch
			{
				null => right,
				JTokenType.Object => UnpatchWithObject(right, (JObject) patch),
				JTokenType.Array => RevertDiff(right, (JArray) patch),
				_ => null
			};


		private JToken UnpatchWithObject(JToken right, JObject patch)
			=> right?.Type == JTokenType.Array && IsArrayPatch(patch)
				? (JToken) ArrayUnpatch((JArray) right, patch)
				: ObjectUnpatch(right as JObject, patch);

		private static JToken RevertDiff(JToken right, JArray patchArray)
		{
			switch (patchArray.Count)
			{
				// Add (we need to remove the property)
				case 1:
					return null;
				// Replace
				case 2:
					return patchArray[0];
				// Delete, Move or TextDiff
				case 3:
				{
					if (patchArray[2].Type != JTokenType.Integer)
						throw new InvalidDataException("Invalid patch object");

					return patchArray[2].Value<int>() switch
					{
						(int) DiffOperation.Deleted => patchArray[0],
						(int) DiffOperation.TextDiff when right.Type != JTokenType.String =>
							throw new InvalidDataException("Invalid patch object"),
						(int) DiffOperation.TextDiff => throw new InvalidDataException(
							"Invalid patch object: TextDiff is not supported."),
						_ => throw new InvalidDataException("Invalid patch object")
					};
				}
				default:
					throw new InvalidDataException("Invalid patch object");
			}
		}

		#region String Overrides

		/// <summary>
		/// Diff two JSON objects.
		/// 
		/// The output is a JObject that contains enough information to represent the
		/// delta between the two objects and to be able perform patch and reverse operations.
		/// </summary>
		/// <param name="left">The base JSON object</param>
		/// <param name="right">The JSON object to compare against the base</param>
		/// <returns>JSON Patch Document</returns>
		public string Diff(string left, string right)
		{
			var obj = Diff(JToken.Parse(left ?? ""), JToken.Parse(right ?? ""));
			return obj?.ToString();
		}

		/// <summary>
		/// Patch a JSON object
		/// </summary>
		/// <param name="left">Unpatched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Patched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public string Patch(string left, string patch)
		{
			var patchedObj = Patch(JToken.Parse(left ?? ""), JToken.Parse(patch ?? ""));
			return patchedObj?.ToString();
		}

		/// <summary>
		/// Unpatch a JSON object
		/// </summary>
		/// <param name="right">Patched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Unpatched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public string Unpatch(string right, string patch)
		{
			var unpatchedObj = Unpatch(JToken.Parse(right ?? ""), JToken.Parse(patch ?? ""));
			return unpatchedObj?.ToString();
		}

		#endregion

		private JObject ObjectDiff(JObject left, JObject right)
		{
			RefreshExclusions();
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));

			var diffPatch = new JObject();

			var leftProperties = new HashSet<string>(StringComparer.Ordinal);

			// Find properties modified or deleted
			foreach (var lp in left.Properties())
			{
				leftProperties.Add(lp.Name);

				//Skip property if in path exclusions
				if (IsPathExcluded(lp.Path)) continue;

				var rp = right.Property(lp.Name);

				// Property deleted
				if (rp == null)
				{
					if (_options.IgnoreMissingProperties) continue;

					diffPatch.Add(new JProperty(lp.Name, new JArray(lp.Value, 0, (int) DiffOperation.Deleted)));
					continue;
				}

				var diff = Diff(lp.Value, rp.Value);
				if (diff != null) diffPatch.Add(new JProperty(lp.Name, diff));
			}

			// Find properties that were added 
			if (!_options.IgnoreNewProperties)
				foreach (var rp in right.Properties().Where(rp => !leftProperties.Contains(rp.Name)))
					diffPatch.Add(new JProperty(rp.Name, new JArray(rp.Value)));

			return diffPatch.Properties().Any() ? diffPatch : null;
		}

		private JObject ArrayDiff(JArray left, JArray right)
		{
			var result = JObject.Parse(@"{ ""_t"": ""a"" }");

			var commonHead = 0;
			var commonTail = 0;

			if (JToken.DeepEquals(left, right))
				return null;

			// Find common head
			while (commonHead < left.Count
			       && commonHead < right.Count
			       && JToken.DeepEquals(left[commonHead], right[commonHead]))
			{
				commonHead++;
			}

			// Find common tail
			while (commonTail + commonHead < left.Count
			       && commonTail + commonHead < right.Count
			       && JToken.DeepEquals(left[left.Count - 1 - commonTail], right[right.Count - 1 - commonTail]))
			{
				commonTail++;
			}

			if (commonHead + commonTail == left.Count)
			{
				// Trivial case, a block (1 or more consecutive items) was added
				for (var index = commonHead; index < right.Count - commonTail; ++index)
				{
					result[$"{index}"] = new JArray(right[index]);
				}

				return result;
			}

			if (commonHead + commonTail == right.Count)
			{
				// Trivial case, a block (1 or more consecutive items) was removed
				for (var index = commonHead; index < left.Count - commonTail; ++index)
				{
					result[$"_{index}"] = new JArray(left[index], 0, (int) DiffOperation.Deleted);
				}

				return result;
			}

			// Complex Diff, find the LCS (Longest Common Subsequence)
			var trimmedLeft = left.ToList().GetRange(commonHead, left.Count - commonTail - commonHead);
			var trimmedRight = right.ToList().GetRange(commonHead, right.Count - commonTail - commonHead);
			var lcs = Lcs.Get(trimmedLeft, trimmedRight);

			for (var index = commonHead; index < left.Count - commonTail; ++index)
			{
				if (lcs.Indices1.IndexOf(index - commonHead) < 0)
				{
					// Removed
					result[$"_{index}"] = new JArray(left[index], 0, (int) DiffOperation.Deleted);
				}
			}

			for (var index = commonHead; index < right.Count - commonTail; index++)
			{
				var indexRight = lcs.Indices2.IndexOf(index - commonHead);

				if (indexRight < 0)
				{
					// Added
					result[$"{index}"] = new JArray(right[index]);
				}
				else
				{
					var li = lcs.Indices1[indexRight] + commonHead;
					var ri = lcs.Indices2[indexRight] + commonHead;

					var diff = Diff(left[li], right[ri]);

					if (diff != null)
					{
						result[$"{index}"] = diff;
					}
				}
			}

			return result;
		}

		private JObject ObjectPatch(JObject obj, JObject patch)
		{
			obj ??= new JObject();
			if (patch == null) return obj;

			var target = (JObject) obj.DeepClone();

			foreach (var diff in patch.Properties())
			{
				var property = target.Property(diff.Name);
				var patchValue = diff.Value;

				// We need to special case deletion when doing objects since a delete is a removal of a property
				// not a null assignment
				if (patchValue.Type == JTokenType.Array && ((JArray) patchValue).Count == 3 &&
				    patchValue[2].Value<int>() == 0)
				{
					target.Remove(diff.Name);
				}
				else
				{
					if (property == null)
					{
						target.Add(new JProperty(diff.Name, Patch(null, patchValue)));
					}
					else
					{
						property.Value = Patch(property.Value, patchValue);
					}
				}
			}

			return target;
		}

		private JArray ArrayPatch(JArray left, JObject patch)
		{
			var toRemove = new List<JProperty>();
			var toInsert = new List<JProperty>();
			var toModify = new List<JProperty>();

			foreach (var op in patch.Properties())
			{
				if (op.Name == "_t")
					continue;

				var value = op.Value as JArray;

				if (op.Name.StartsWith("_"))
				{
					// removed item from original array
					if (value != null && value.Count == 3 && (value[2].ToObject<int>() == (int) DiffOperation.Deleted ||
					                                          value[2].ToObject<int>() ==
					                                          (int) DiffOperation.ArrayMove))
					{
						toRemove.Add(new JProperty(op.Name.Substring(1), op.Value));

						if (value[2].ToObject<int>() == (int) DiffOperation.ArrayMove)
							toInsert.Add(new JProperty(value[1].ToObject<int>().ToString(),
								new JArray(left[int.Parse(op.Name.Substring(1))].DeepClone())));
					}
					else
					{
						throw new Exception(
							$"Only removal or move can be applied at original array indices. Context: {value}");
					}
				}
				else
				{
					if (value != null && value.Count == 1)
					{
						toInsert.Add(op);
					}
					else
					{
						toModify.Add(op);
					}
				}
			}


			// remove items, in reverse order to avoid sawing our own floor
			toRemove.Sort((x, y) => int.Parse(x.Name).CompareTo(int.Parse(y.Name)));
			for (var i = toRemove.Count - 1; i >= 0; --i)
			{
				var op = toRemove[i];
				left.RemoveAt(int.Parse(op.Name));
			}

			// insert items, in reverse order to avoid moving our own floor
			toInsert.Sort((x, y) => int.Parse(y.Name).CompareTo(int.Parse(x.Name)));
			for (var i = toInsert.Count - 1; i >= 0; --i)
			{
				var op = toInsert[i];
				left.Insert(int.Parse(op.Name), ((JArray) op.Value)[0]);
			}

			foreach (var op in toModify)
			{
				var p = Patch(left[int.Parse(op.Name)], op.Value);
				left[int.Parse(op.Name)] = p;
			}

			return left;
		}

		private JObject ObjectUnpatch(JObject obj, JObject patch)
		{
			if (obj == null)
				obj = new JObject();
			if (patch == null)
				return obj;

			var target = (JObject) obj.DeepClone();

			foreach (var diff in patch.Properties())
			{
				var property = target.Property(diff.Name);
				var patchValue = diff.Value;

				// We need to special case addition when doing objects since an undo add is a removal of a property
				// not a null assignment
				if (patchValue.Type == JTokenType.Array && ((JArray) patchValue).Count == 1)
				{
					target.Remove(property.Name);
				}
				else
				{
					if (property == null)
					{
						target.Add(new JProperty(diff.Name, Unpatch(null, patchValue)));
					}
					else
					{
						property.Value = Unpatch(property.Value, patchValue);
					}
				}
			}

			return target;
		}

		private JArray ArrayUnpatch(JArray right, JObject patch)
		{
			var toRemove = new List<JProperty>();
			var toInsert = new List<JProperty>();
			var toModify = new List<JProperty>();

			foreach (var op in patch.Properties())
			{
				if (op.Name == "_t")
					continue;

				var value = op.Value as JArray;

				if (op.Name.StartsWith("_"))
				{
					// removed item from original array
					if (value != null && value.Count == 3 && (value[2].ToObject<int>() == (int) DiffOperation.Deleted ||
					                                          value[2].ToObject<int>() ==
					                                          (int) DiffOperation.ArrayMove))
					{
						var newOp = new JProperty(value[1].ToObject<int>().ToString(), op.Value);

						if (value[2].ToObject<int>() == (int) DiffOperation.ArrayMove)
						{
							toInsert.Add(new JProperty(op.Name.Substring(1),
								new JArray(right[value[1].ToObject<int>()].DeepClone())));
							toRemove.Add(newOp);
						}
						else
						{
							toInsert.Add(new JProperty(op.Name.Substring(1), new JArray(value[0])));
						}
					}
					else
					{
						throw new Exception(
							$"Only removal or move can be applied at original array indices. Context: {value}");
					}
				}
				else
				{
					if (value != null && value.Count == 1)
					{
						toRemove.Add(op);
					}
					else
					{
						toModify.Add(op);
					}
				}
			}

			// first modify entries
			foreach (var op in toModify)
			{
				var p = Unpatch(right[int.Parse(op.Name)], op.Value);
				right[int.Parse(op.Name)] = p;
			}

			// remove items, in reverse order to avoid sawing our own floor
			toRemove.Sort((x, y) => int.Parse(x.Name).CompareTo(int.Parse(y.Name)));
			for (var i = toRemove.Count - 1; i >= 0; --i)
			{
				var op = toRemove[i];
				right.RemoveAt(int.Parse(op.Name));
			}

			// insert items, in reverse order to avoid moving our own floor
			toInsert.Sort((x, y) => int.Parse(x.Name).CompareTo(int.Parse(y.Name)));
			foreach (var op in toInsert)
			{
				right.Insert(int.Parse(op.Name), ((JArray) op.Value)[0]);
			}

			return right;
		}

		private static bool IsArrayPatch(JObject patch)
		{
			// an array diff is an object with a property "_t" with value "a"
			var arrayDiffCanary = patch.Property("_t");

			return arrayDiffCanary?.Value.Type == JTokenType.String &&
			       arrayDiffCanary.Value.ToObject<string>() == "a";
		}
	}
}
