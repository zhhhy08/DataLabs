namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArmThrottle;

    [TestClass]
    public class ArmSubscriptionRateLimitingUtilsTest
    {
        [DataTestMethod]
        [DataRow("00000000-0000-0000-0000-000000000000", "/subscriptions/00000000-0000-0000-0000-000000000000/SubscriptionRateLimit")]
        public void TestGenerateArmSubscriptionRateLimitingResourceId(
           string subscription,
           string expectedRateLimitResource)
        {
            var result = ARMThrottleManager.GenerateArmSubscriptionRateLimitingResourceId(subscription);
            Assert.AreEqual(expectedRateLimitResource, result);
        }
    }
}
