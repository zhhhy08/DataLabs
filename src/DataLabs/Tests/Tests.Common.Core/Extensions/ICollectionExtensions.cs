namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    [TestClass]
    public class ICollectionExtensions
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
        public void AddRangeTest()
        {
            ICollection<string> source = new List<string>()
            { "a", "b", "c" };

            IEnumerable<string> values = new List<string>()
            { "d", "e", "f" };

            source.AddRange(values);

            Assert.AreEqual(6, source.Count);
        }
    }
}

