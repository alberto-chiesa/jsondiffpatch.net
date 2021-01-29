using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace JsonDiffPatchDotNet.UnitTests
{
	[TestFixture]
	public class PatchUnitTests
	{
		public JsonDiffPatch Jdp { get; set; }

		[SetUp]
		public void Setup()
		{
			Jdp = new JsonDiffPatch();
		}

		[Test, TestCaseSource(nameof(PatchShouldApplyComplexPatchesCases))]
		public void PatchSpecificTests(string leftJson, string diffJson, string rightJson)
		{
			var left = JToken.Parse(leftJson);
			var right = JToken.Parse(rightJson);
			var diff = JToken.Parse(diffJson); // Jdp.Diff(left, right);
			var patched = Jdp.Patch(left, diff);

			// patched should be equal to the "right" value
			Assert.That(patched.ToString(Formatting.None), Is.EqualTo(right.ToString(Formatting.None)));
		}

		public static IEnumerable<TestCaseData> PatchShouldApplyComplexPatchesCases()
		{
			yield return new TestCaseData(
				@"[0,1,2,3,4,5,6,7,8,9,10]",
				@"{
                    ""8"": [88],
                    ""_3"": ["""", 10, 3],
                    ""_7"": ["""", 3, 3],
                    ""_8"": [8, 0, 0],
                    ""_10"": ["""", 0, 3],
                    ""_t"": ""a""
                  }",
				@"[10,0,1,7,2,4,5,6,88,9,3]"
			).SetName("Array Moving test");
		}

		[Test]
		public void Patch_ArrayPatchMoving_Success()
		{
			var jdp = new JsonDiffPatch(new DiffOptions());
			var left = JToken.Parse(@"[0,1,2,3,4,5,6,7,8,9,10]");
			var right = JToken.Parse(@"[10,0,1,7,2,4,5,6,88,9,3]");
			var patch = JToken.Parse(
				@"{ ""8"": [88], ""_t"": ""a"", ""_3"": ["""", 10, 3], ""_7"": ["""", 3, 3], ""_8"": [8, 0, 0], ""_10"": ["""", 0, 3] }");

			var patched = jdp.Patch(left, patch);

			Assert.AreEqual(right.ToString(), patched.ToString());
		}

		[Test]
		public void Patch_ArrayPatchMovingNonConsecutive_Success()
		{
			var jdp = new JsonDiffPatch(new DiffOptions());
			var left = JToken.Parse(@"[0,1,3,4,5]");
			var right = JToken.Parse(@"[0,4,3,1,5]");
			var patch = JToken.Parse(@"{""_t"": ""a"", ""_2"": ["""", 2, 3],""_3"": ["""", 1, 3]}");

			var patched = jdp.Patch(left, patch);

			Assert.AreEqual(right.ToString(), patched.ToString());
		}

		[Test]
		public void Patch_ArrayPatchMoveDeletingNonConsecutive_Success()
		{
			var jdp = new JsonDiffPatch(new DiffOptions());
			var left = JToken.Parse(@"[0,1,3,4,5]");
			var right = JToken.Parse(@"[0,5,3]");
			var patch = JToken.Parse(@"{""_t"": ""a"", ""_1"": [ 1, 0, 0], ""_3"": [4,0, 0],""_4"": [ """", 1, 3 ]}");

			var patched = jdp.Patch(left, patch);

			Assert.AreEqual(right.ToString(), patched.ToString());
		}

		[Test]
		public void Patch_Bug17EfficienText_Success()
		{
			var jdp = new JsonDiffPatch();
			var left = JToken.Parse(
				"{ \"key\": \"aaaa aaaaaa aaaa aaaaaaa: aaaaaaaaa aaaa aaaaaaaa aaaa: 31-aaa-2017 aaaaa aaaaa aaaaaaa aaaa aaaaaaaa aaaa: 31-aaa-2017aaaaaa aaaaaa: aaaaaaaaa aaaaaa: aaaaaa aaaaa aaaaa aaaaaaa aaaaaa: aaaaaaaaaa aaaaaaaa aaaaaaa: aaaa(aaaa aaaaaa/aaaaaaaaaaaa)-aaaaaaa(aaaaaaaaaa/aaaaaaaaaa aaaa aaaaaa)aaaaa aaaaa aaaaaaa:aaaaaa aaaaaaa: aaaaaaaa aaaaaaaaaa aaaaaaa: aaaaaaaaaaaa aaaaaaaaaa: aaaaaaaa-aaaaaaaaaaaaaaaa aaaaaaaaaa aaaaaa: aaaaaaaaaaaaaaaa aaaaaaaaaa aaaaa: aaaaaaaa aaaaa-aaaaa aaaaaaaaaa, aaaaa aaaaaaa aa aaaaaaa aaaaaaaaaaaa aaaaa aaaaaaaaaaa (aaaaaa), aaaaa a 100 aaaaa aa aaa aaaaaaa.aaa aaaa: aaaaaaaaaaaaaaaa: aaaaaaaaaaaa aaaaaaaa: aaa aaaaa aaaaa:aaaaaaa aaaaaaa: 31-aaa-2014aaaaaa aaaaa: 16-aaa-2016aaaaaa aaaaa: 30-aaa-2017aaaaaa aaaaa: 27-aaa-2017aaaaaa aaaaa: 31-aaa-2017aa aaaaaaaaaa aaaaaaaaaa, (aaaaa aa aaaa aa a 52.67 aaaaa aa aaaa aaa aaaa aaaaaa aaaaaa), aaaaa 100 aa aaaa aaaaaaa.aaaaaaa aaaaaaa: 16-aaa-2016aaaa aaaaaaa aa 100 aaaaa aa aaaa aaa aaaa aaaaaa aaa aa aaaaaaaaaa aaa aaaaaaaaaa, a 88.02 aaaaaaaaaa aa aaaa aaa aaaa aaaaaa aaaaaa.aaaaaaa aaaaaaa: 30-aaa-2017aaaa aaaaaaa aa 100 aaaaa aa aaa-aaaa aaaaaa, aaaaa aa 100 aaaaa aa aaaa aaa aaaa aaaaaa aaaaaaaaaaaa aaaaaaaaaaaaaaaaaaaaa aaaaaaaaaaaa aaa, aaaaa aa aaaa aa 65.656 aaaaa aa aaaa aaa aaaa aaaaaa aaa aa aaaaaaaaaa aaa aaaaaaaaaa aaa 34.343 aa aaaa aaa aaaa aaaaaa aaaaaa. aaaa aaa aaaa aaaaaa aaa aa aaaaaaaaaa aaa aaaaaaaaaa aa 88.02 aaaaa aa aaaa aaa aaaa aaaaaa aaaaaa.aaaaaaa aaaaaaa: 27-aaa-2017aaaa aaaaaaa aa 100 aaaaa aa aaa-aaaa aaaaaa, aaaaa\" }");
			var right = JToken.Parse(
				"{ \"key\": \"aaaa aaaaaa aaaa aaaaaaa: aaaaaaaaa aaaa aaaaaaaa aaaa: 17-aaa-2017 aaaaa aaaaa aaaaaaa aaaa aaaaaaaa aaaa: 17-aaa-2017aaaaaa aaaaaa: aaaaaaaaa aaaaaa: aaaaaa aaaaa aaaaa aaaaaaa aaaaaa: aaaaaaaaaa aaaaaaaa aaaaaaa: aaaa(aaaa aaaaaa/aaaaaaaaaaaa)-aaaaaaa(aaaaaaaaaa/aaaaaaaaaa aaaa aaaaaa)aaaaa aaaaa aaaaaaa:aaaaaa aaaaaaa: aaaaaaaa aaaaaaaaaa aaaaaaa: aaaaaaaaaaaa aaaaaaaaaa: aaaaaaaa-aaaaaaaaaaaaaaaa aaaaaaaaaa aaaaaa: aaaaaaaaaaaaaaaa aaaaaaaaaa aaaaa aaaa: -2016aaaaaaaaa aaaaaaaaaa aaaaa: aaaa aaaaaaa aa 100 aaaaa aa aaa-aaaa aaaaaa aaa, aaaaaaaa aaaaaaaaaa aa aaaaaa.aaaaaaaaa aaaaaaaaaa: aaaaaaaa-aaaaaaaaaaaaaaaa aaaaaaaaaa aaaaaa: aaaaaaaaaaaaa aaaaaaaaaa aa aaaa: -2016aaaaaaaaa aaaaaaaaaa aaaaa: aaaaaaaa aaaaa-aaaaa aaaaaaaaaa, aaaaa aaaaaaa aa aaaaaaa aaaaaaaaaaaa aaaaa aaaaaaaaaaa (aaaaaa), aaaaa a 100 aaaaa aa aaa aaaaaaa.aaa aaaa: aaaaaaaaaaaaaaaa: aaaaaaaaaaaa aaaaaaaa: aaa aaaaa aaaaa:aaaaaaa aaaaaaa: 31-aaa-2014aaaaaa aaaaa: 16-aaa-2016aaaaaa aaaaa: 30-aaa-2017aaaaaa aaaaa: 27-aaa-2017aaaaaa aaaaa: 31-aaa-2017aaaaaa aaaaa: 16-aaa-2017aa aaaaaaaaaa aaaaaaaaaa, (aaaaa aa aaaa aa a 52.67 aaaaa aa aaaa aaa aaaa aaaaaa aaaaaa), aaaaa 100 aa aaaa aaaaaaa.aaaaaaa aaaaaaa: 16-aaa-2016aaaa\" }");
			JToken patch = jdp.Diff(left, right);

			var patched = jdp.Patch(left, patch);

			Assert.IsTrue(JToken.DeepEquals(right.ToString(), patched.ToString()));
		}

		[Test]
		public void Diff_ExcludePaths_ValidPatch()
		{
			var jdp = new JsonDiffPatch(new DiffOptions {ExcludePaths = {"id", "nested.id"}});
			var left = JObject.Parse(
				@"{ ""id"": ""pid"", ""p"": ""old"", ""nested"": { ""id"":""nid"", ""p"":""old"" } }");
			var right = JObject.Parse(
				@"{ ""id"": ""pid2"", ""p"": ""new"", ""nested"": { ""id"":""nid2"", ""p"":""new"" } }");
			var expected =
				JObject.Parse(@"{ ""id"": ""pid"", ""p"": ""new"", ""nested"": { ""id"":""nid"", ""p"":""new"" } }");
			var patch = jdp.Diff(left, right);
			var patched = jdp.Patch(left, patch) as JObject;

			Assert.AreEqual(expected.ToString(), patched.ToString());
		}

		[Test]
		public void Diff_Behaviors_IgnoreMissingProperties_ValidPatch()
		{
			var jdp = new JsonDiffPatch(new DiffOptions {DiffBehaviors = DiffBehavior.IgnoreMissingProperties});
			var left = JObject.Parse(
				@"{ ""id"": ""pid"", ""p"": ""old"", ""nested"": { ""id"":""nid"", ""p"":""old"" } }");
			var right = JObject.Parse(@"{ ""p"": ""new"", ""nested"": { ""p"":""new"" }, ""newP"": ""new"" }");
			var expected =
				JObject.Parse(
					@"{ ""id"": ""pid"", ""p"": ""new"", ""nested"": { ""id"":""nid"", ""p"":""new"" }, ""newP"": ""new"" }");
			var patch = jdp.Diff(left, right);
			var patched = jdp.Patch(left, patch) as JObject;

			Assert.AreEqual(expected.ToString(), patched.ToString());
		}

		[Test]
		public void Diff_Behaviors_IgnoreNewProperties_ValidPatch()
		{
			var jdp = new JsonDiffPatch(new DiffOptions {DiffBehaviors = DiffBehavior.IgnoreNewProperties});
			var left = JObject.Parse(
				@"{ ""id"": ""pid"", ""p"": ""old"", ""nested"": { ""id"":""nid"", ""p"":""old"" } }");
			var right = JObject.Parse(
				@"{ ""id"": ""pid2"", ""p"": ""new"", ""nested"": { ""id"":""nid2"", ""p"":""new"" }, ""newP"": ""new"" }");
			var expected =
				JObject.Parse(@"{ ""id"": ""pid2"", ""p"": ""new"", ""nested"": { ""id"":""nid2"", ""p"":""new"" } }");
			var patch = jdp.Diff(left, right);
			var patched = jdp.Patch(left, patch) as JObject;

			Assert.AreEqual(expected.ToString(), patched.ToString());
		}

		[Test]
		public void Diff_ExludeAndBehaviors_ExcludeIgnoreMissingIgnoreNew_ValidPatch()
		{
			var jdp = new JsonDiffPatch(new DiffOptions
			{
				ExcludePaths = {"id", "nested.id"},
				DiffBehaviors = DiffBehavior.IgnoreMissingProperties | DiffBehavior.IgnoreNewProperties
			});
			var left = JObject.Parse(
				@"{ ""id"": ""pid"", ""p"": ""old"", ""nested"": { ""id"":""nid"", ""p"":""old"" } }");
			var right = JObject.Parse(@"{ ""id"": ""pid2"", ""nested"": { ""id"":""nid2"" }, ""newP"": ""new"" }");
			var expected =
				JObject.Parse(@"{ ""id"": ""pid"", ""p"": ""old"", ""nested"": { ""id"":""nid"", ""p"":""old"" } }");
			var patch = jdp.Diff(left, right);
			var patched = jdp.Patch(left, patch) as JObject;

			Assert.AreEqual(expected.ToString(), patched.ToString());
		}
	}
}
