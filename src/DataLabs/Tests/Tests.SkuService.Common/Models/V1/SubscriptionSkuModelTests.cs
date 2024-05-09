namespace Tests.SkuService.Common.Models.V1
{
    using global::SkuService.Common.Models.V1;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class SubscriptionSkuModelTests
    {
        [TestMethod]
        public void SubscriptionSkuModelStringTest()
        {
            var subscriptionSkuModel = new SubscriptionSkuModel()
            {
                Skus = [],
                ResourceType = "Microsoft.ResourceGraph/skuProviders/resourceTypes/locations/skus",
                Location = "WestUS",
                SkuProvider = "Microsoft.Compute"
            };
            var strSubscriptionSkuModel = subscriptionSkuModel.ToString();
            var obj = JsonConvert.DeserializeObject<SubscriptionSkuModel>(strSubscriptionSkuModel);
            Assert.AreEqual(subscriptionSkuModel.ResourceType, obj?.ResourceType);
            Assert.AreEqual(subscriptionSkuModel.SkuProvider, obj?.SkuProvider);
        }
    }
}
