using System;
using JsonDiffPatchDotNet.Internals;
using Newtonsoft.Json.Linq;

namespace JsonDiffPatchDotNet
{
	public class JsonDiffPatch
	{
		private readonly DiffOptions _diffOptions;

		public JsonDiffPatch() : this(new DiffOptions())
		{
		}

		public JsonDiffPatch(DiffOptions options)
		{
			if (options == null) throw new ArgumentNullException(nameof(options));
			_diffOptions = options;
		}

		/// <summary>
		/// Diff two JSON objects.
		/// 
		/// The output is a JObject that contains enough information to represent the
		/// delta between the two objects and to be able perform patch and reverse operations.
		/// </summary>
		/// <param name="left">The base JSON object</param>
		/// <param name="right">The JSON object to compare against the base</param>
		/// <returns>JSON Patch Document</returns>
		public string Diff(string left, string right) => Diff(left.ToJToken(), right.ToJToken())?.ToString();

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
			_diffOptions.RefreshExclusions();
			return DiffAlgorithm.Diff(left, right, _diffOptions);
		}

		/// <summary>Patches a JSON object </summary>
		/// <param name="left">Unpatched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Patched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public string Patch(string left, string patch) => Patch(left.ToJToken(), patch.ToJToken())?.ToString();

		/// <summary>Patches a JSON object</summary>
		/// <param name="left">Unpatched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Patched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public JToken Patch(JToken left, JToken patch) => PatchAlgorithm.Patch(left?.DeepClone(), patch);

		/// <summary>Unpatches a JSON object.</summary>
		/// <param name="right">Patched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Unpatched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public string Unpatch(string right, string patch) => Unpatch(right.ToJToken(), patch.ToJToken())?.ToString();

		/// <summary>Unpatches a JSON object.</summary>
		/// <param name="right">Patched JSON object</param>
		/// <param name="patch">JSON Patch Document</param>
		/// <returns>Unpatched JSON object</returns>
		/// <exception cref="System.IO.InvalidDataException">Thrown if the patch document is invalid</exception>
		public JToken Unpatch(JToken right, JToken patch) => UnpatchAlgorithm.Unpatch(right?.DeepClone(), patch);
	}
}
