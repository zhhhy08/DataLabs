namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [TestClass]
    public class SolutionLoggingUtilsTests
    {
        [TestMethod]
        public void TestHidSigFromBlobUri()
        {
            // sig in the end
            var blobUri = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sp=r&sig=anySignature";
            var expected = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sp=r&sig=MASKED";

            var result = SolutionLoggingUtils.HideSigFromBlobUri(blobUri);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TestHidSigFromBlobUri2()
        {
            // sig in the middle
            var blobUri = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sig=anySignature&sp=r";
            var expected = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sig=MASKED&sp=r";

            var result = SolutionLoggingUtils.HideSigFromBlobUri(blobUri);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TestHidSigFromBlobUri3()
        {
            // No sig
            var blobUri = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sp=r";
            var expected = "https://test/test.json?skoid=test-test&sktid=test-test&skt=test-test&ske=test&sks=b&skv=2023-01-03&sv=2023-01-03&st=2023-08-02T09%3A00%3A00Z&se=2023-08-09T08%3A00%3A00Z&sr=b&sp=r";

            var result = SolutionLoggingUtils.HideSigFromBlobUri(blobUri);
            Assert.AreEqual(expected, result);
        }
    }
}
