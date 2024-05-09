namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ArnNotificationClient
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient;
    using System;

    [TestClass]
    public class ArnNotificationClientLoggerTests
    {
        #region Tests

        [TestMethod]
        public void TestLogger()
        {
            var logger = new ArnNotificationClientLogger(true, true);
            var exception = new Exception("test");

            logger.Debug(Guid.NewGuid(), "test");
            logger.Debug(Guid.NewGuid(), "test", exception);
            logger.Info(Guid.NewGuid(), "test");
            logger.Info(Guid.NewGuid(), "test", exception);
            logger.Warn(Guid.NewGuid(), "test");
            logger.Warn(Guid.NewGuid(), "test", exception);
            logger.Error(Guid.NewGuid(), "test");
            logger.Error(Guid.NewGuid(), "test", exception);
            logger.Fatal(Guid.NewGuid(), "test");
            logger.Fatal(Guid.NewGuid(), "test", exception);
        }

        #endregion
    }
}
