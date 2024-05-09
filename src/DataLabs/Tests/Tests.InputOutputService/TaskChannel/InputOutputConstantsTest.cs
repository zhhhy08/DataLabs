namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.IOService.TaskChannel
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common;

    [TestClass]
    public class InputOutputConstantsTest
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
        public void CheckDuplicatedInputOutputConstants()
        {
            // Check if there is any duplicated values
            var allowedKeys = new HashSet<string>()
            {

            };

            AssertUtils.HasDuplicatedValue(typeof(InputOutputConstants), allowedKeys);
        }

        [TestMethod]
        public void CheckDuplicatedSolutionConstants()
        {
            // Check if there is any duplicated values
            var allowedKeys = new HashSet<string>()
            {
                "ExceptionColumn",
                "Exception"
            };

            AssertUtils.HasDuplicatedValue(typeof(SolutionConstants), allowedKeys);
        }
    }
}

