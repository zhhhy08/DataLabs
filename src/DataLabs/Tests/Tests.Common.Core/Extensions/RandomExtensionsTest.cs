namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    [TestClass]
    public class RandomExtensionsTest
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
        public void NextDoubleTest()
        {
            var random = new Random();
            var result = random.NextDouble(minValue: 1.0d, maxValue: 2.0d);
            Assert.IsTrue(result >= 1.0d && result < 2.0d);
        }
    }
}

