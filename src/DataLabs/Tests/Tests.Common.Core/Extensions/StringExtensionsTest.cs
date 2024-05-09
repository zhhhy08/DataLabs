namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    [TestClass]
    public class StringExtensionsTest
    {
        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        [TestMethod]
        public void ContainsIgnoreCaseTest()
        {
            var str = "myTestString";
            Assert.AreEqual(true, str.ContainsIgnoreCase("test"));
            Assert.AreEqual(false, str.Contains("test"));
        }

        [TestMethod]
        public void SafeIsValidGuidTest()
        {
            var guid = Guid.NewGuid();
            Assert.AreEqual(true, guid.ToString().SafeIsValidGuid());
            Assert.AreEqual(false, "InvalidGuid".SafeIsValidGuid());
        }

        [TestMethod]
        public void IsOrdinalMatchTest()
        {
            var str1 = "myTestString";
            var str2 = "myteststring";
            Assert.AreEqual(true, str1.IsOrdinalMatch(str2));
            Assert.AreEqual(false, str1.Equals(str2));
        }

        [TestMethod]
        public void CharacterOccurrenceCountTest()
        {
            var str = "myTeststring";
            Assert.AreEqual(1, str.CharacterOccurrenceCount('T'));
            Assert.AreEqual(2, str.CharacterOccurrenceCount('s'));
        }

        [TestMethod]
        public void SplitAndRemoveEmptyTest()
        {
            var str = "/my/Test//string/";

            var result = str.SplitAndRemoveEmpty('/');
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("my", result[0]);
            Assert.AreEqual("Test", result[1]);
            Assert.AreEqual("string", result[2]);
        }

        [TestMethod]
        public void FastSplitAndReturnFirstTest()
        {
            var str = "my/Teststring";
            var result = str.FastSplitAndReturnFirst('/');
            Assert.AreEqual("my", result);

            str = "myTest";
            result = str.FastSplitAndReturnFirst('/');
            Assert.AreEqual(str, result);
        }
    }
}

