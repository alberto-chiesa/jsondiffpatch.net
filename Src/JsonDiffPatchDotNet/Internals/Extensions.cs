// /////////////////////////////////////////////////////////////////////////////
// 
// File:                JsonExtensions.cs
// 
// Copyright (c) 2021 SEA Vision srl
// This File is a property of SEA Vision srl
// Any use or duplication of this file or part of it,
// is strictly prohibited without a written permission
// 
// /////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace JsonDiffPatchDotNet.Internals
{
	public static class Extensions
	{
		public static JToken ToJToken(this string s) => JToken.Parse(s ?? "");

		public static void Do<T>(this IEnumerable<T> enumerable, Action<T> f)
		{
			foreach (var item in enumerable) f(item);
		}

		public static int Index(this JProperty p) => int.Parse(p.Name);
		public static int LIndex(this JProperty p) => int.Parse(p.Name.Substring(1));
	
		// an array diff is an object with a property "_t" with value "a"
		public static bool IsArrayPatch(this JToken patch)
		{
			var canary = (patch as JObject)?.Property("_t");
			return canary?.Value.Type == JTokenType.String && canary.Value.ToObject<string>() == "a";
		}
	}
}
