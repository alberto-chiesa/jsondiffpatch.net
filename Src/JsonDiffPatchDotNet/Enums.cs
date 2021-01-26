using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonDiffPatchDotNet
{
	public enum ArrayDiffMode
	{
		/// <summary>
		/// Efficient array diff does a deep examination of the contents of an array and 
		/// produces a patch document that only contains elements in the array that were 
		/// added or removed. Efficient array diff can only patch and unpatch the original 
		/// JSON array values used to produce the patch or there will be unintended 
		/// consequences.
		/// </summary>
		Efficient,

		/// <summary>
		/// Simple array diff does an exact match comparison on two arrays. If they are different
		/// the entire left and entire right arrays are added to the patch document as a simple
		/// JSON token replace. If they are the same, then token is skipped in the patch document.
		/// </summary>
		Simple,
	}

	[Flags]
	public enum DiffBehavior
	{
		/// <summary>
		/// Default behavior
		/// </summary>
		None,

		/// <summary>
		/// If the patch document is missing properties that are in the source document, leave the existing properties in place instead of deleting them
		/// </summary>
		IgnoreMissingProperties,

		/// <summary>
		/// If the patch document contains properties that aren't defined in the source document, ignore them instead of adding them
		/// </summary>
		IgnoreNewProperties
	}

	enum DiffOperation
	{
		Deleted = 0,

		// this should not be present, because DiffMatchPatch has been removed.
		TextDiff = 2,

		ArrayMove = 3,
	}
}
