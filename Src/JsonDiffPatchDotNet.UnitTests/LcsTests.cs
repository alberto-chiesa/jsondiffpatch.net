// /////////////////////////////////////////////////////////////////////////////
// 
// File:                LcsTests.cs
// 
// Copyright (c) 2021 SEA Vision srl
// This File is a property of SEA Vision srl
// Any use or duplication of this file or part of it,
// is strictly prohibited without a written permission
// 
// /////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;
using JsonDiffPatchDotNet.Internals;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace JsonDiffPatchDotNet.UnitTests
{
    [TestFixture]
    public class LcsTests
    {
        [Test, TestCaseSource(nameof(TestLcsCases))]
        public void LcsTest(string leftJson, string rightJson, ArrayDiff expected)
        {
            var left = leftJson == null ? null : JArray.Parse(leftJson);
            var right = rightJson == null ? null : JArray.Parse(rightJson);

            var actualLcs = DiffAlgorithm.CompareArrays(left.ToList(), right.ToList(), 0);

            CompareLcs(expected, actualLcs);
        }

        public static IEnumerable<TestCaseData> TestLcsCases()
        {
            yield return new TestCaseData(
                @"[]", @"[1,2]", new ArrayDiff()
            ).SetName("Empty left array");

            yield return new TestCaseData(
                @"[1,2]", @"[]", new ArrayDiff()
            ).SetName("Empty right array");

            yield return new TestCaseData(
                @"[1,2,3,10,4,1]",
                @"[1,5,10,6,1]",
                new ArrayDiff(
                    new ItemPair(0, 0),
                    new ItemPair(3, 2),
                    new ItemPair(5, 4)
                )
            ).SetName("Arrays with gaps.");

            yield return new TestCaseData(
                @"[1,1,2,3,4,1,1]",
                @"[1,2,3,1]",
                new ArrayDiff(
                    new ItemPair(1, 0),
                    new ItemPair(2, 1),
                    new ItemPair(3, 2),
                    new ItemPair(6, 3)
                )).SetName("Arrays with repeated values favor last one.");

            yield return new TestCaseData(
                @"[1,2,3,1]",
                @"[1,1,2,3,4,1,1]",
                new ArrayDiff(
                    new ItemPair(0, 1),
                    new ItemPair(1, 2),
                    new ItemPair(2, 3),
                    new ItemPair(3, 6)
                )
                {

                }).SetName("Arrays with repeated values favor last one.");

            yield return new TestCaseData(
                @"[ 0, 1, 2, 3, 4, 5, 6, 7, 8]",
                @"[ 0, 1,12, 3,14, 5,16, 7,18]",
                new ArrayDiff(
                    new ItemPair(0, 0),
                    new ItemPair(1, 1),
                    new ItemPair(3, 3),
                    new ItemPair(5, 5),
                    new ItemPair(7, 7)
                )).SetName("Subsequence on arrays with same length.");

            yield return new TestCaseData(
                @"[1,2,3,4,5,6]",
                @"[4,5,6,1,2,3]",
                new ArrayDiff(
                    new ItemPair(3, 0),
                    new ItemPair(4, 1),
                    new ItemPair(5, 2)
                )).SetName("Arrays with same length privilege last sequence on left - 1");

            yield return new TestCaseData(
                @"[1,2,4,5]",
                @"[7,8,9,4,5,1,2]",
                new ArrayDiff(
                    new ItemPair(2, 3),
                    new ItemPair(3, 4)
                )).SetName("Arrays with same length privilege last sequence on left - 1");

            yield return new TestCaseData(
                @"[1,2,3,4]",
                @"[4,3,2,1]",
                new ArrayDiff(
                    new ItemPair(3, 0)
                )
                {
                    ToMove = new []
                    {
                        new ItemPair(0, 3),
                        new ItemPair(1, 2),
                        new ItemPair(2, 1)
                    }
                }).SetName("Reversed array");
        }

        private void CompareLcs(ArrayDiff expected, ArrayDiff actual)
        {
            Assert.That(actual.Lcs.Count, Is.EqualTo(expected.Lcs.Length));
            for (var i = 0; i < actual.Lcs.Length; i++)
            {
                Assert.That(
                    FormatSequenceItem(actual.Lcs[i]),
                    Is.EqualTo(FormatSequenceItem(expected.Lcs[i]))
                );
            }

            if (expected.ToMove != null)
            {
                Assert.That(actual.ToMove.Count, Is.EqualTo(expected.ToMove.Length));
                for (var i = 0; i < actual.ToMove.Length; i++)
                {
                    Assert.That(
                        FormatSequenceItem(actual.ToMove[i]),
                        Is.EqualTo(FormatSequenceItem(expected.ToMove[i]))
                    );
                }
            }
        }

        private static string FormatSequenceItem(ItemPair sequenceItemPair)
        {
            return $"{sequenceItemPair.LeftIndex}->{sequenceItemPair.RightIndex}";
        }
    }
}
