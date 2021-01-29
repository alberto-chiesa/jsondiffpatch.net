// /////////////////////////////////////////////////////////////////////////////
// 
// File:                PatchImplementation.cs
// 
// Copyright (c) 2021 SEA Vision srl
// This File is a property of SEA Vision srl
// Any use or duplication of this file or part of it,
// is strictly prohibited without a written permission
// 
// /////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using static JsonDiffPatchDotNet.DiffOperation;

namespace JsonDiffPatchDotNet.Internals
{
	public static class PatchAlgorithm
	{
		public static JToken Patch(JToken left, JToken patch)
			=> patch?.Type switch
			{
				null => left,
				JTokenType.Object => left?.Type == JTokenType.Array && patch.IsArrayPatch()
					? PatchArray((JArray) left, (JObject) patch)
					: PatchObject(left as JObject, (JObject) patch),
				JTokenType.Array => PatchValue((JArray) patch),
                JTokenType.String when patch.ToString() == "" => left,
				_ => throw new InvalidDataException("Invalid patch object")
			};

		private static JToken PatchValue(JArray patch)
		{
			if (patch.Count > 3) throw new InvalidDataException("Invalid patch object");
			if (patch.Count == 1) return patch[0];
			if (patch.Count == 2) return patch[1];
			if (patch[2].Type != JTokenType.Integer) throw new InvalidDataException("Invalid patch object");
			if (patch[2].Value<int>() == (int) DiffOperation.Deleted) return null;
			throw new InvalidDataException("Invalid patch object");
		}

		private static JToken PatchObject(JObject obj, JObject patch)
		{
			obj ??= new JObject();
			if (patch == null) return obj;

			var target = obj;
			//var target = (JObject) obj.DeepClone();

			foreach (var diff in patch.Properties())
			{
				var property = target.Property(diff.Name);
				var patchValue = diff.Value;

				// We need to special case deletion when doing objects since a delete is a removal of a property
				// not a null assignment
				if ((patchValue as JArray)?.Count == 3 && patchValue[2].Value<int>() == 0) target.Remove(diff.Name);
				else if (property == null) target.Add(new JProperty(diff.Name, Patch(null, patchValue)));
				else property.Value = Patch(property.Value, patchValue);
			}

			return target;
		}

		private static JToken PatchArray(JArray left, JObject patch)
		{
			var toRemove = new List<int>();
			var toInsert = new List<KeyValuePair<int, JToken>>();
			var toModify = new List<KeyValuePair<int, JToken>>();

			foreach (var op in patch.Properties().Where(p => p.Name != "_t"))
			{
				var value = op.Value as JArray;

				// removed item from original array
				if (op.Name.StartsWith("_"))
				{
					var idx = op.LIndex();
					var msg = "Only removal or move can be applied at original array indices. Context: {0}";
					if (value == null || value.Count != 3) throw new Exception(string.Format(msg, value));

					var diffOperation = (DiffOperation) value[2].ToObject<int>();

					if (diffOperation != Deleted && diffOperation != ArrayMove)
						throw new Exception(string.Format(msg, value));

					toRemove.Add(idx);

					if (diffOperation == ArrayMove)
						toInsert.Add(Pair(value[1].ToObject<int>(), Patch(left[idx].DeepClone(), value[0])));
				}
				else if (value != null && value.Count == 1) toInsert.Add(Pair(op.Index(), value[0]));
				else toModify.Add(Pair(op.Index(), op.Value));
			}


			// remove items in reverse order, then insert, then modify
			toRemove.OrderByDescending(x => x).Do(left.RemoveAt);
			toInsert.OrderBy(x => x.Key).Do(p => left.Insert(p.Key, p.Value));
			toModify.ForEach(p => left[p.Key] = Patch(left[p.Key], p.Value));

			return left;
		}

		public static KeyValuePair<TK, TV> Pair<TK, TV>(TK key, TV value) => new KeyValuePair<TK, TV>(key, value);
	}
}
