namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using System.Collections.Generic;

    [TestClass]
    public class IDictionaryExtensionsTest
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
        public void CoalesceDictionaryTest()
        {
            Dictionary<string, string> dictionary = null;
            dictionary = dictionary.CoalesceDictionary();
            dictionary["key"] = "value";
            Assert.AreEqual(1, dictionary.Count);
        }
    }
}

