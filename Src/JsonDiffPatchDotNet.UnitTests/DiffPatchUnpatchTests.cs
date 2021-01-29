using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace JsonDiffPatchDotNet.UnitTests
{
    public static class JtokenExtensions
    {
        public static string AsString(this JToken jToken) => jToken?.ToString(Formatting.None);
    }

    [TestFixture]
    public class DiffPatchUnpatchTests
    {
        [Test]
        public void Diff_EfficientArrayDiffHugeArrays_NoStackOverflow()
        {
            JToken HugeArrayFunc(int start, int count)
                => JToken.Parse($"[{string.Join(",", Enumerable.Range(start, count))}]");

            const int arraySize = 10;

            var jdp = new JsonDiffPatch();
            var left = HugeArrayFunc(0, arraySize);
            var right = HugeArrayFunc(arraySize / 2, arraySize);

            var sw = new Stopwatch();
            sw.Start();
            var diff = jdp.Diff(left, right);
            var restored = jdp.Patch(left, diff);
            sw.Stop();
            Console.WriteLine("Executed array Diff in {0}", sw.ElapsedMilliseconds);
            Assert.That(JToken.DeepEquals(restored, right));
        }

        [Test, TestCaseSource(nameof(TestDiffingCases))]
        public void TestDiffing(string leftJson, string rightJson, string expected)
        {
            var jdp = new JsonDiffPatch();
            var left = leftJson == null ? null : JToken.Parse(leftJson);
            var originalLeft = left.AsString();
            var right = rightJson == null ? null : JToken.Parse(rightJson);
            var originalRight = right.AsString();

            var diff = jdp.Diff(left, right);

            Console.WriteLine("Calculated diff:");
            Console.WriteLine(diff?.ToString(Formatting.Indented));

            if (expected == null)
            {
                Assert.That(diff, Is.Null);
            }
            else
            {
                var exp = JToken.Parse(expected);
                if (!JToken.DeepEquals(diff, exp))
                {
                    Console.WriteLine("Diff object:");
                    Console.WriteLine(diff == null ? "null" : diff.ToString(Formatting.Indented));
                    Assert.Fail("Diff is not equal to expected.");
                }
            }

            // check there are not side effects
            Assert.That(left.AsString(), Is.EqualTo(originalLeft));
            Assert.That(right.AsString(), Is.EqualTo(originalRight));
        }

        [Test, TestCaseSource(nameof(TestDiffingCases))]
        public void TestPatching(string leftJson, string rightJson, string diffJson)
        {
            var jdp = new JsonDiffPatch();
            var left = leftJson == null ? null : JToken.Parse(leftJson);
            var originalLeft = left.AsString();
            var diff = diffJson == null ? null : JToken.Parse(diffJson);
            var originalDiff = diff.AsString();

            var patched = jdp.Patch(left, diff);
            // patched should be equal to the "right" value
            if (rightJson == null)
            {
                Assert.That(patched, Is.EqualTo(""));
            }
            else
            {
                var areEqual = JToken.DeepEquals(patched, JToken.Parse(rightJson));
                if (!areEqual)
                    Assert.Fail("The patched value does not match the expected \"right\" value.\n{0}",
                        patched.ToString(Formatting.Indented)
                    );
            }

            // check there are not side effects
            Assert.That(left?.ToString(Formatting.None), Is.EqualTo(originalLeft));
            Assert.That(diff?.ToString(Formatting.None), Is.EqualTo(originalDiff));
        }

        [Test, TestCaseSource(nameof(TestDiffingCases))]
        public void TestUnpatching(string leftJson, string rightJson, string diffJson)
        {
            var jdp = new JsonDiffPatch();
            var right = rightJson == null ? null : JToken.Parse(rightJson);
            var diff = diffJson == null ? null : JToken.Parse(diffJson);
            var originalRight = right.AsString();
            var originalDiff = diff.AsString();

            var unpatched = jdp.Unpatch(right, diff);
            // unpatched should be equal to the "left" value
            if (leftJson == null)
            {
                Assert.That(unpatched, Is.EqualTo(""));
            }
            else
            {
                var areEqual = JToken.DeepEquals(unpatched, JToken.Parse(leftJson));
                if (!areEqual)
                    Assert.Fail("The unpatched value does not match the expected \"left\" value.\n{0}",
                        unpatched.ToString(Formatting.Indented)
                    );
            }

            // check there are not side effects
            Assert.That(right?.ToString(Formatting.None), Is.EqualTo(originalRight));
            Assert.That(diff?.ToString(Formatting.None), Is.EqualTo(originalDiff));
        }

        public static IEnumerable<TestCaseData> TestDiffingCases()
        {
            var testCounter = 0;
            string Count() => (++testCounter).ToString("D2") + ". ";

            yield return new TestCaseData("{}", "{}", null)
                .SetName(Count() + "Empty objects give null diff.");

            yield return new TestCaseData("{\"p\": true }", "{\"p\": true }", null)
                .SetName(Count() + "Equal Boolean properties give null diff.");

            yield return new TestCaseData("{\"p\": true }", "{\"p\": false }", "{\"p\": [true, false] }")
                .SetName(Count() + "Different Boolean properties");

            yield return new TestCaseData("{\"p\": true }", "{}", "{\"p\": [true, 0, 0] }")
                .SetName(Count() + "Boolean property deleted");

            yield return new TestCaseData("{}", "{\"p\": true }", "{\"p\": [true] }")
                .SetName(Count() + "Boolean property Added");

            yield return new TestCaseData(
                    @"{ ""p"" : ""bla1h111111111111112312weldjidjoijfoiewjfoiefjefijfoejoijfiwoejfiewjfiwejfowjwifewjfejdewdwdewqwertyqwertifwiejifoiwfei"" }",
                    @"{ ""p"" : ""blah1"" }",
                    @"{ ""p"" : [""bla1h111111111111112312weldjidjoijfoiewjfoiefjefijfoejoijfiwoejfiewjfiwejfowjwifewjfejdewdwdewqwertyqwertifwiejifoiwfei"", ""blah1""] }")
                .SetName(Count() + "Edit text property");

            yield return new TestCaseData(
                    @"{ ""p"" : ""text"" }",
                    @"{ ""p"" : null }",
                    @"{ ""p"" : [""text"", null] }")
                .SetName(Count() + "Edit text property to null");

            yield return new TestCaseData(
                @"{ ""p"" : {
                          ""p"": 1,
                          ""ToDelete"": 1,
                          ""j"": [0, 2, 4],
                          ""k"": [1]
                      } }",
                @"{ ""p"" : {
                          ""p"": ""New Value"",
                          ""j"": [0, 2, 3],
                          ""k"": null,
                          ""Added"": 2
                      } }",
                @"{ ""p"": {
                      ""p"": [1, ""New Value""],
                      ""ToDelete"": [1,0,0],
                      ""j"": {
                        ""_t"": ""a"",
                        ""2"": [4, 3]
                      },
                      ""k"": [[1], null],
                      ""Added"": [2]
                } }").SetName(Count() + "Complex nested editing");

            yield return new TestCaseData(
                    @"{""p"": [1,2,[1],false,""11111"",3,{""p"":false},10,10] }",
                    @"{""p"": [1,2,[1,3],false,""11112"",3,{""p"":true},10,10] }",
                    @"{ ""p"": {
                      ""_t"": ""a"",
                      ""2"": {
                        ""_t"": ""a"",
                        ""1"": [3],
                      },
                      ""4"": [""11111"", ""11112""],
                      ""6"":{""p"": [false, true] }
                } }")
                .SetName(Count() + "Complex array edit");

            //yield return new TestCaseData(
            //		@"{""p"": [1,2,[1],false,""11111"",3,{""p"":false},10,10] }",
            //		@"{""p"": [1,2,[1,3],false,""11112"",3,{""p"":true},10,10] }",
            //		@"{ ""p"": {
            //                   ""_t"": ""a"",
            //                   ""2"": {
            //                     ""_t"": ""a"",
            //                     ""1"": [3],
            //                   },
            //                   ""4"": [""11111"", ""11112""],
            //                   ""6"":{""p"": [false, true] }
            //             } }")
            //	.SetName(Count() + "Complex array edit");

            yield return new TestCaseData("1", "\"hello\"", "[1, \"hello\"]")
                .SetName(Count() + "Different value type");

            yield return new TestCaseData(null, "{}", "[\"\", {}]")
                .SetName(Count() + "Left side null is treated like empty string");

            yield return new TestCaseData("{}", null, "[{}, \"\"]")
                .SetName(Count() + "Right side null is treated as empty string");

            yield return new TestCaseData("[1,2,3]", "[1,2,3]", null)
                .SetName(Count() + "Equals array give null diff");

            yield return new TestCaseData(
                "[1,2,3,4]", "[2,3,4]",
                @"{""_t"":""a"", ""_0"": [1, 0, 0] }"
            ).SetName(Count() + "Array diff removed head");

            yield return new TestCaseData(
                "[1, 2, 3, 4]", "[1, 2, 3]",
                @"{""_t"": ""a"", ""_3"": [4, 0, 0] }"
            ).SetName(Count() + "Array diff removed tail");

            yield return new TestCaseData(
                "[1,2,3,4]", "[1,2]",
                @"{""_t"":""a"", ""_2"": [3, 0, 0], ""_3"": [4, 0, 0] }"
            ).SetName(Count() + "Array diff removed longer tail");

            yield return new TestCaseData(
                "[1,2,3,4]", "[1,2]",
                @"{""_t"":""a"", ""_2"": [3, 0, 0], ""_3"": [4, 0, 0] }"
            ).SetName(Count() + "Array diff removed longer tail");

            yield return new TestCaseData(
                "[1,2,3,4]", "[0,1,2,3,4]",
                @"{""_t"":""a"", ""0"": [0]}"
            ).SetName(Count() + "Array diff Different head");

            yield return new TestCaseData(
                "[1,2,3,4]", "[1,2,3,4,5]",
                @"{""_t"":""a"", ""4"": [5]}"
            ).SetName(Count() + "Array diff Different tail");

            yield return new TestCaseData(
                "[1,2,3,4]", "[0,1,2,3,4,5]",
                @"{""_t"":""a"", ""0"": [0], ""5"": [5]}"
            ).SetName(Count() + "Array diff Different head and tail");

            yield return new TestCaseData(
                @"[1,2,{""p"":false},4]", @"[1,2,{""p"":true},4]",
                @"{""_t"":""a"", ""2"": {""p"": [false, true] }}"
            ).SetName(Count() + "Array diff Same length nested property");

            var complexObjJson = @"{ ""@context"": [
                    ""http://www.w3.org/ns/csvw"",
                    { ""@language"": ""en"", ""@base"": ""http://example.org"" }
                ] }";

            yield return new TestCaseData(complexObjJson, complexObjJson, null)
                .SetName(Count() + "Complex array diff same object is null");
        }
    }
}
