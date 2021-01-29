// /////////////////////////////////////////////////////////////////////////////
// 
// File:                DiffImplementation.cs
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ReSharper disable CoVariantArrayConversion

namespace JsonDiffPatchDotNet.Internals
{
    public static class DiffAlgorithm
    {
        public static JToken Diff(JToken left, JToken right, DiffOptions options)
        {
            left ??= new JValue("");
            right ??= new JValue("");

            if (left.Type == JTokenType.Object && right.Type == JTokenType.Object)
                return ObjectDiff((JObject) left, (JObject) right, options);

            if (left.Type == JTokenType.Array && right.Type == JTokenType.Array)
                return ArrayDiff((JArray) left, (JArray) right, options);

            return JToken.DeepEquals(left, right) ? null : new JArray(left, right);
        }

        private static JObject ObjectDiff(JObject left, JObject right, DiffOptions options)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            var diff = new JObject();

            // Find properties modified or deleted
            foreach (var leftProp in left.Properties())
            {
                //Skip property if in path exclusions
                if (options.IsPathExcluded(leftProp.Path)) continue;

                var rightProp = right[leftProp.Name];

                // Property was present. Format diff
                if (rightProp != null)
                {
                    var propDiff = Diff(leftProp.Value, rightProp, options);
                    if (propDiff != null) diff.Add(new JProperty(leftProp.Name, propDiff));
                }
                // Property deleted
                else if (!options.IgnoreMissingProperties)
                    diff.Add(new JProperty(leftProp.Name, new JArray(leftProp.Value, 0, (int) DiffOperation.Deleted)));
            }

            // Find properties that were added 
            if (!options.IgnoreNewProperties)
                foreach (var rp in right.Properties().Where(rp => left[rp.Name] == null))
                    diff.Add(new JProperty(rp.Name, new JArray(rp.Value)));

            return diff.Properties().Any() ? diff : null;
        }

        private static JObject ArrayDiff(JArray left, JArray right, DiffOptions options)
        {
            if (JToken.DeepEquals(left, right)) return null;

            var head = GetCommonHeadLength(left, right);
            var tail = GetCommonTailLength(left, right, head);

            // Complex Diff, find the LCS (Longest Common Subsequence) in the arrays stripped of head and tail
            // trimmedLeft and trimmedRight are the left and right arrays, trimmed of common heads and tails.
            var trimmedLeft = left.Skip(head).Take(left.Count - tail - head).ToList();
            var trimmedRight = right.Skip(head).Take(right.Count - tail - head).ToList();
            var arrayDiff = CompareArrays(trimmedLeft, trimmedRight, head);

            var result = new List<JProperty> {new JProperty("_t", "a")};
            result.AddRange((arrayDiff.ToDiff.Concat(arrayDiff.ToMove))
                .Select(pair =>
                {
                    var valueDiff = Diff(left[pair.LeftIndex], right[pair.RightIndex], options);
                    return (pair.LeftIndex == pair.RightIndex)
                        ? new JProperty($"{pair.RightIndex}", valueDiff)
                        : new JProperty($"_{pair.LeftIndex}",
                            new JArray(valueDiff, pair.RightIndex, (int) DiffOperation.ArrayMove)
                        );
                })
            );
            result.AddRange(arrayDiff.ToAdd.Select(idx => MakeAddDiff(idx, right)));
            result.AddRange(arrayDiff.ToRemove.Select(idx => MakeDeletionDiff(idx, left)));
            //for (var index = head; index < right.Count - tail; index++)
            //{
            //    var item = arrayDiff.Lcs.FirstOrDefault(x => x.RightIndex == index);

            //    // Every element in the right array that is not in the LCS gets added
            //    if (item != null)
            //    {
            //        var diff = Diff(left[item.LeftIndex], right[item.RightIndex], options);
            //        if (diff != null) result.Add(new JProperty($"{index}", diff));
            //    }
            //}

            return new JObject(result.ToArray());
        }

        private static JProperty MakeAddDiff(int index, JArray right)
            => new JProperty($"{index}", new JArray(right[index]));

        private static JProperty MakeDeletionDiff(int index, JArray left)
            => new JProperty($"_{index}", new JArray(left[index], 0, 0));

        // elements in the head of the 2 arrays that are equal
        private static int GetCommonHeadLength(JArray left, JArray right)
            => left.Zip(right, JToken.DeepEquals)
                .TakeWhile(areEqual => areEqual)
                .Take(left.Count > right.Count ? right.Count : left.Count)
                .Count();

        // elements in the tail of the 2 arrays that are equal
        private static int GetCommonTailLength(JArray left, JArray right, int commonHead)
            => left.Reverse().Zip(right.Reverse(), JToken.DeepEquals)
                .TakeWhile(areEqual => areEqual)
                .Take((left.Count > right.Count ? right.Count : left.Count) - commonHead)
                .Count();

        internal static ArrayDiff CompareArrays(List<JToken> left, List<JToken> right, int head)
        {
            var leftCount = left.Count;
            var rightCount = right.Count;
            if (leftCount == 0)
                return new ArrayDiff
                {
                    ToMove = new ItemPair[0], ToDiff = new ItemPair[0],
                    ToAdd = Enumerable.Range(head, rightCount).ToArray(), ToRemove = new int[0],
                };

            if (rightCount == 0)
                return new ArrayDiff
                {
                    ToMove = new ItemPair[0], ToDiff = new ItemPair[0],
                    ToAdd = new int[0], ToRemove = Enumerable.Range(head, leftCount).ToArray()
                };

            // each equalityMatrix[i][j] is true if left[i] deep equals right[j]
            var equalityMatrix = CalculateEqualityMatrix(left, right);
            var matrix = CalculateLcsMatrix(equalityMatrix, leftCount, rightCount);
            var lcsLength = matrix[leftCount, rightCount];

            var result = new ArrayDiff(lcsLength);

            // sets containing the indices still to be moved
            var leftIdxs = new HashSet<int>();
            var rightIdxs = new HashSet<int>();
            // backtrack
            int i, j, sequenceIdx = lcsLength - 1;
            for (i = leftCount - 1, j = rightCount - 1; i >= 0 && j >= 0;)
            {
                // Console.WriteLine("{0},{1}", i, j);
                // If the JSON tokens at the same position are both Objects or both Arrays, we just say they 
                // are the same even if they are not, because we can package smaller deltas than an entire 
                // object or array replacement by doing object to object or array to array diff.
                // if true, we found an item in the LCS
                if (equalityMatrix[i, j])
                {
                    result.Lcs[sequenceIdx--] = new ItemPair(head + i--, head + j--);
                    continue;
                }

                if (matrix[i, j + 1] > matrix[i + 1, j]) leftIdxs.Add(i--);
                else rightIdxs.Add(j--);
            }

            while (i >= 0) leftIdxs.Add(i--);
            while (j >= 0) rightIdxs.Add(j--);

            result.ToMove = FindMovements(leftIdxs, rightIdxs, equalityMatrix, head).ToArray();
            result.ToDiff = FindEdits(leftIdxs, rightIdxs, head).ToArray();
            result.ToRemove = leftIdxs.Select(x => head + x).OrderBy(x => x).ToArray();
            result.ToAdd = rightIdxs.Select(x => head + x).OrderBy(x => x).ToArray();

            return result;
        }

        // leftIdxs and rightIdxs now contains every "i" and "j" value not in the LCS
        // now we search for move operations
        private static IEnumerable<ItemPair> FindEdits(HashSet<int> leftIdxs, HashSet<int> rightIdxs, int head)
        {
            var leftSorted = leftIdxs.OrderBy(x => x);
            var rightSorted = rightIdxs.OrderBy(x => x);
            var pairs = leftSorted.Zip(rightSorted, (i, j) => new {Left = i, Right = j}).ToList();
            foreach (var pair in pairs)
            {
                leftIdxs.Remove(pair.Left);
                rightIdxs.Remove(pair.Right);
                yield return new ItemPair(head + pair.Left, head + pair.Right);
            }
        }

        // leftIdxs and rightIdxs now contains every "i" and "j" value not in the LCS
        // now we search for move operations
        private static IEnumerable<ItemPair> FindMovements(
            HashSet<int> leftIdxs, HashSet<int> rightIdxs, bool[,] equalityMatrix, int head)
        {
            foreach (var iToMove in leftIdxs.OrderBy(x => x).ToList())
            {
                var targetJ = rightIdxs.Select(j => (int?) j).FirstOrDefault(j => equalityMatrix[iToMove, j.Value]);
                if (targetJ != null)
                {
                    leftIdxs.Remove(iToMove);
                    rightIdxs.Remove(targetJ.Value);
                    yield return new ItemPair(head + iToMove, head + targetJ.Value);
                }
            }
        }

        // https://en.wikipedia.org/wiki/Longest_common_subsequence_problem#LCS_function_defined
        private static int[,] CalculateLcsMatrix(bool[,] equalityMatrix, int leftCount, int rightCount)
        {
            var matrix = new int[leftCount + 1, rightCount + 1];
            for (var i = 0; i < leftCount; i++)
            for (var j = 0; j < rightCount; j++)
            {
                matrix[i + 1, j + 1] = equalityMatrix[i, j]
                    ? matrix[i, j] + 1
                    : Math.Max(matrix[i, j + 1], matrix[i + 1, j]);
            }

            // PrintoutLcsMatrix(leftCount, rightCount, matrix);
            return matrix;
        }

#if DEBUG
        private static void PrintoutLcsMatrix(int leftCount, int rightCount, int[,] matrix)
        {
            Console.WriteLine("Matrix:");
            for (var i = 0; i < leftCount + 1; i++)
            {
                for (var j = 0; j < rightCount + 1; j++) Console.Write(matrix[i, j].ToString("D2") + ",");
                Console.WriteLine();
            }

            Console.WriteLine();
        }
#endif

        private static bool[,] CalculateEqualityMatrix(List<JToken> left, List<JToken> right)
        {
            int leftCount = left.Count, rightCount = right.Count;
            // each equalityMatrix[i][j] is true if left[i] deep equals right[j]
            var equalityMatrix = new bool[leftCount, rightCount];

            // evaluate equalities using strings (it's faster)
            var leftJson = left.Select(x => x.ToString(Formatting.None)).ToList();
            var rightJson = right.Select(x => x.ToString(Formatting.None)).ToList();
            int i, j;
            for (i = 0; i < leftCount; i++)
            for (j = 0; j < rightCount; j++)
            {
                equalityMatrix[i, j] = leftJson[i] == rightJson[j];
            }

            return equalityMatrix;
        }
    }

    /// <summary>Describes the difference between two array,
    /// containing data about array move operations, about the
    /// LCS, and about items to edit.</summary>
    public class ArrayDiff
    {
        // Pairs in the Longest Common Subsequence will NOT be outputted in the diff
        public ItemPair[] Lcs { get; set; }
        public int[] ToRemove { get; set; }
        public int[] ToAdd { get; set; }
        public ItemPair[] ToMove { get; set; }
        public ItemPair[] ToDiff { get; set; }

        public ArrayDiff(params ItemPair[] items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            Lcs = items;
        }

        public ArrayDiff(int lcsLength)
        {
            if (lcsLength < 0) throw new ArgumentOutOfRangeException(nameof(lcsLength));

            Lcs = new ItemPair[lcsLength];
        }
    }

    public class ItemPair
    {
        public int LeftIndex { get; }
        public int RightIndex { get; }

        public ItemPair(int leftIndex, int rightIndex)
        {
            LeftIndex = leftIndex;
            RightIndex = rightIndex;
        }
    }
}
