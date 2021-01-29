// /////////////////////////////////////////////////////////////////////////////
// 
// File:                UnpatchImplementation.cs
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
	public static class UnpatchAlgorithm
	{
		public static JToken Unpatch(JToken right, JToken patch)
			=> patch?.Type switch
			{
				null => right,
				JTokenType.Object => right?.Type == JTokenType.Array && patch.IsArrayPatch()
					? UnpatchArray((JArray) right, (JObject) patch)
					: UnpatchObject(right as JObject, (JObject) patch),
				JTokenType.Array => UnpatchValue((JArray) patch),
				_ => null
			};

		private static JToken UnpatchValue(JArray patch)
		{
			if (patch.Count > 3) throw new InvalidDataException("Invalid patch object");
			if (patch.Count == 1) return null;
			if (patch.Count == 2) return patch[0];
			if (patch[2].Type != JTokenType.Integer) throw new InvalidDataException("Invalid patch object");
			if (patch[2].Value<int>() == (int) Deleted) return patch[0];
			throw new InvalidDataException("Invalid patch object");
		}

		private static JToken UnpatchObject(JObject obj, JObject patch)
		{
			obj ??= new JObject();
			if (patch == null) return obj;

			var target = (JObject) obj.DeepClone();

			foreach (var diff in patch.Properties())
			{
				var property = target.Property(diff.Name);
				var patchValue = diff.Value;

				// We need to special case addition when doing objects since an undo add is a removal of a property
				// not a null assignment
				if (patchValue.Type == JTokenType.Array && ((JArray) patchValue).Count == 1)
					target.Remove(property.Name);
				else if (property == null) target.Add(new JProperty(diff.Name, Unpatch(null, patchValue)));
				else property.Value = Unpatch(property.Value, patchValue);
			}

			return target;
		}

		private static JToken UnpatchArray(JArray right, JObject patch)
		{
			var toRemove = new List<int>();
			var toInsert = new List<KeyValuePair<int, JToken>>();
			var toModify = new List<KeyValuePair<int, JToken>>();

			foreach (var op in patch.Properties().Where(p => p.Name != "_t"))
			{
				var value = op.Value as JArray;

				if (op.Name.StartsWith("_"))
				{
					var msg = "Only removal or move can be applied at original array indices. Context: {0}";
					// removed item from original array
					if (value == null || value.Count != 3) throw new Exception(string.Format(msg, value));

					var diffOperation = (DiffOperation) value[2].ToObject<int>();
					if (diffOperation != Deleted && diffOperation != ArrayMove)
						throw new Exception(string.Format(msg, value));

					var index = value[1].ToObject<int>();

					if (diffOperation == ArrayMove)
					{
						toInsert.Add(Pair(op.LIndex(), right[index].DeepClone()));
						toRemove.Add(index);
					}
					else toInsert.Add(Pair(op.LIndex(), value[0]));
				}
				else if (value != null && value.Count == 1) toRemove.Add(op.Index());
				else toModify.Add(Pair(op.Index(), op.Value));
			}

			// first modify entries, then remove in reverse order, then insert
			toModify.ForEach(p => right[p.Key] = Unpatch(right[p.Key], p.Value));
			toRemove.OrderByDescending(x => x).Do(right.RemoveAt);
			toInsert.OrderBy(x => x.Key).Do(p => right.Insert(p.Key, p.Value));

			return right;
		}

		public static KeyValuePair<TK, TV> Pair<TK, TV>(TK key, TV value) => new KeyValuePair<TK, TV>(key, value);
	}
}
