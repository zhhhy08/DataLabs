namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Exceptions
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [TestClass]
    public class DataLabsExceptionsTests
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
        public void NotAllowedTypeExceptionTest()
        {
            Exception ex = new NotAllowedTypeException("test");
            Assert.AreEqual(SolutionUtils.GetExceptionTypeSimpleName(ex), nameof(NotAllowedTypeException));
        }

        [TestMethod]
        public void NotAllowedPartnerExceptionTest()
        {
            Exception ex = new NotAllowedPartnerException("test");
            Assert.AreEqual(SolutionUtils.GetExceptionTypeSimpleName(ex), nameof(NotAllowedPartnerException));
        }

        [TestMethod]
        public void NotAllowedClientIdExceptionTest()
        {
            Exception ex = new NotAllowedClientIdException("test");
            Assert.AreEqual(SolutionUtils.GetExceptionTypeSimpleName(ex), nameof(NotAllowedClientIdException));
        }

        [TestMethod]
        public void BadRequestExceptionTest()
        {
            Exception ex = new ResourceFetcherBadRequestException("test");
            Assert.AreEqual(SolutionUtils.GetExceptionTypeSimpleName(ex), nameof(ResourceFetcherBadRequestException));
        }
    }
}