// /////////////////////////////////////////////////////////////////////////////
// 
// File:                DiffOptions.cs
// 
// Copyright (c) 2021 SEA Vision srl
// This File is a property of SEA Vision srl
// Any use or duplication of this file or part of it,
// is strictly prohibited without a written permission
// 
// /////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace JsonDiffPatchDotNet
{
	public sealed class DiffOptions
	{
		public DiffOptions() : this(DiffBehavior.None, new List<string>())
		{
		}

		public DiffOptions(DiffBehavior diffBehaviors, List<string> excludePaths)
		{
			if (excludePaths == null) throw new ArgumentNullException(nameof(excludePaths));
			DiffBehaviors = diffBehaviors;
			ExcludePaths = excludePaths;
		}

		/// <summary>Specifies which paths to exclude from the diff patch set.</summary>
		public List<string> ExcludePaths { get; }

		/// <summary>Specifies behaviors to apply to the diff patch set.</summary>
		public DiffBehavior DiffBehaviors { get; set; }

		public bool IgnoreMissingProperties => DiffBehaviors.HasFlag(DiffBehavior.IgnoreMissingProperties);
		public bool IgnoreNewProperties => DiffBehaviors.HasFlag(DiffBehavior.IgnoreNewProperties);

		private HashSet<string> _exclusions = new HashSet<string>();

		public void RefreshExclusions()
			=> _exclusions = new HashSet<string>(
				ExcludePaths ?? Enumerable.Empty<string>(),
				StringComparer.OrdinalIgnoreCase
			);

		public bool IsPathExcluded(string path) => path != null && _exclusions.Contains(path);
	}
}
