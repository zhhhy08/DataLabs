namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.TrafficTuner
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;

    [TestClass]
    public class TrafficTunerTests
    {
        private const string TrafficTunerRuleKey = "TrafficTunerRuleKey";

        [TestMethod]
        public void TestEvaluateTunerResult_ConfigMapNotPresent()
        {
            SetupConfig(string.Empty);

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(default);

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_AllowAllTenants()
        {
            SetupConfig("allowAllTenants: true");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(default);

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_StopAllTenants()
        {
            SetupConfig("stopAllTenants: true");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(default);

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.StopAllTenants, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_UpdateStopAllTenantsViaMethod()
        {
            SetupConfig("allowAllTenants: true;stopAllTenants: false;includedregions: westus");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            tuner.UpdateTrafficTunerRuleValue("allowAllTenants: false;stopAllTenants: true;includedregions: westus");

            var result = tuner.EvaluateTunerResult(default);

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.StopAllTenants, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_UpdateAllowAllTenantsViaMethod()
        {
            SetupConfig("allowAllTenants: false;stopAllTenants: false;includedregions: westus");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            tuner.UpdateTrafficTunerRuleValue("allowAllTenants: true;stopAllTenants: false;includedregions: westus");

            var result = tuner.EvaluateTunerResult(default);

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_MessageRetryCutoffExceeded()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"messageretrycutoffcount: 12;includedsubscriptions: {tenant}={subscription1},{subscription2}");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(messageRetryCount: 16));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.MessageRetryCount, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludedSubscriptions()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription1},{subscription2};" +
                $"messageretrycutoffcount: 12");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_AllowAllSubscriptionsInATenant()
        {
            string tenant1 = Guid.NewGuid().ToString();
            string tenant2 = Guid.NewGuid().ToString();
            string tenant3 = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();
            string subscription3 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant1}|{tenant2}|{tenant3}={subscription2},{subscription3};" +
                $"messageretrycutoffcount: 12");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant1,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant2,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant3,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant3,
                subscriptionId: subscription2,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_SameTenantIncludedAndExcludedSubscriptions()
        {
            string tenant1 = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions:{tenant1}={subscription1};excludedsubscriptions:{tenant1}={subscription2}" +
                $"messageretrycutoffcount:12");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant1,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant1,
                subscriptionId: subscription2,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_ExcludedSubscriptions()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();
            string subscription3 = Guid.NewGuid().ToString();

            SetupConfig($"excludedsubscriptions: {tenant}={subscription1},{subscription2},{subscription3};" +
                $"messageretrycutoffcount: 12");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant,
                subscriptionId: subscription3,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.SubscriptionId, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_NotIncludedTenants()
        {
            string tenant1 = Guid.NewGuid().ToString();
            string tenant2 = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();
            string subscription3 = Guid.NewGuid().ToString();

            SetupConfig($"excludedsubscriptions: {tenant1}={subscription3};" +
                $"includedsubscriptions: {tenant1}={subscription1},{subscription2};" +
                $"messageretrycutoffcount: 12");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant1,
                subscriptionId: subscription3,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.SubscriptionId, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant1,
                subscriptionId: subscription1,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant2,
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
        }

        [TestMethod]
        public void Test_ParseTrafficTunerConfig_Prod()
        {
            SetupConfig("allowalltenants: false; stopalltenants: false; includedregions:; includedsubscriptions: ac0173a8-d04e-4a4a-a726-75a9ac42ee2f| 0598535e-625d-4d43-86bb-5240e2e61ee0| bcb80eda-c944-4481-b417-3a3d39dcc8ef| ff20dd64-5699-48f1-b8f3-2ff0031bb8a4| dcfe39b2-cb4f-48d0-8f79-72a5c83181e0| 09d39d92-f9ae-4c79-9aa3-ff375ac5e6eb| 5e6d7959-d83c-418e-bc9a-c1766178f93d| 7392aafd-1b2a-46f7-81a9-ab0e2f0e2146| f5472a57-ef2c-4de6-ab0b-636fac2f72a2| cd725094-3fab-4e54-b5ef-7d2db58414e9| 5aea9a56-fb63-4da0-9bcb-33181cc1a75f| eedc068c-502f-4632-b7a3-ad64e74c5f6f| 5c8085d9-1e88-4bb6-b5bd-e6e6d5b5babd| cde969f5-34e4-44a9-ad66-040c406312b5| e73756cc-67a0-4dfe-ae7f-c68ee6bb4c20| f72683d1-f405-42bf-a291-8e6032c6ad7c| e4f80c4f-d183-46cc-baa5-3141bca1ced3| 9c8d7196-e4cf-4c75-9312-3b2fc26f41e6| beaab1c6-c6ce-4750-92da-cac20ae5d13a| 4ba61381-36f9-41d7-84d3-76c0466122a5| d65b03ed-6a7d-41ca-a17d-4798d70d1d3f| 2463cfda-1c0b-43f5-b6e5-1c370752bb93| 7e3b4781-bf47-428f-8e56-65e6e14b3523| 69163aa6-843d-4f8d-ab67-c789b6a42c61| 3078684f-d143-440a-ae40-d391a79950b1| ce63ec06-27e0-45bd-b275-9097dd25c71d| 541af648-cbc0-4151-bdfb-0cd8359b44f6| e68d09d4-980c-476b-bf25-7f5b6ec0dda2| a016d4bb-3446-43f5-a89c-1d9e089c97e7| c4f1bf21-2674-4c18-9fa4-1a5cef2451c7| 5734e837-adc9-4cf1-a10f-42302d037078| 78dd488d-892f-4724-9861-9486288f62b8| d052cda8-b9fd-4ace-a47e-12b89ca2027d| 08dd0e3c-939c-4ce2-a38a-a9ad61b98b1a| 1afe3c3a-61ad-4ab3-8b6c-721f2b7f8d9b| 208742e0-248c-4f2f-a088-b98f7e0e69b4| 31325ef4-9fd9-4925-a2c6-058ffafed6f7| 3287f50e-681b-4c84-802b-47c752366a6a| 39dcd234-91e1-4943-b0a6-6f2a6654dc40| 3a036fb1-e5f8-4ebc-8a0f-c5b52042f98c| 4b2462a4-bbee-495a-a0e1-f23ae524cc9c| 4f6c9546-1c94-471d-a6ea-b04ef8c9c0b9| 5109c5dc-8203-43b8-bafd-84c47a54521f| 5eae8bbe-41cb-442e-a176-b65891241527| 6199ada3-095d-4116-ad33-5f90396ab36c| 64bbe726-057f-4cbb-a714-b60bbdb9f6ef| 6c5d07dd-5d1c-46fe-b5ba-11b422ebb6a0| 727de601-0ed3-4f9b-b61d-923221cfdde3| 7d2ca40f-bde1-4805-ae45-176b941b182a| 8a35e8cd-119a-4446-b762-5002cf925b1d| 9290db31-b72d-4e2e-9da8-c575999c94b2| 97b391e9-ac22-49ea-8c59-911a715441e4| 98ad103d-bc7c-44ec-b169-22f8543c27ba| a1c31400-b7ce-4e5b-8ed8-c07337039682| a336a5ae-2015-4b70-9316-78b8d4cdabf5| a62b18b1-16b4-4760-b047-48e530bc66ca| ac9b5ee7-7811-4917-b3eb-57efb1426983| c26915cd-e63d-43e4-92e0-f1a927cf28ee| c6536249-3d78-41eb-8ad4-0ca774963f1f| ced4dda9-bfa5-441b-8918-1be493c2831b| ed4498f5-1913-4192-8f51-9c2a7622b88f| 752c0bb0-43bf-434d-bf7f-f2608e2ab8d0| c041c852-b7f8-4d22-9230-3d649a4c4695| 16b3c013-d300-468d-ac64-7eda0820b6d3| fd91810c-57b4-43e3-b513-c2a81e8d6a27| e0793d39-0939-496d-b129-198edd916feb| fb6ea403-7cf1-4905-810a-fe5547e98204| 9f077186-2f24-4255-9a46-d0e841a7eadf| fc9c759a-338b-4757-8eb1-d05549b347a9| c8eca3ca-1276-46d5-9d9d-a0f2a028920f| e6d706ba-6154-4227-83f9-4dcec31058a6| 10c5dfa7-b5c3-4cf2-9265-f0e32a960967| 031e4f4a-6a35-45e0-a98e-107900032d62| 4341df80-fbe6-41bf-89b0-e6e2379c9c23 = f5e78916-7d8f-4dc5-899a-9192f62f7179, 34bc02b8-c11b-46e9-8a81-8ed918aa7e2d| 2376575d-0ebf-4a45-87a3-d66d6f10d569 = 0f17be51-e7d1-4e18-8768-628192afa5e9| 66e853de-ece3-44dd-9d66-ee6bdf4159d4 = fe4f67f6-2f0d-43a3-bc7b-1dfb48e14121| 513294a0-3e20-41b2-a970-6d30bf1546fa = 673e9aa5-17ac-425e-b0b4-e72fd2e74195| 588e8fae-da1b-4c8b-810f-3229806c04fe = 5ba1c37e-1548-41e0-9f76-1e525d31d02a| 16af8cf7-eeb9-4533-a377-abac8f72cc4e = 3cfe1671-b864-47a2-878e-449d6cd4c860| 7d76d45a-a201-4a68-bf3a-597f0a5fa533 = beae7fdc-4aed-4fc2-aa72-88c299589093, 1b0f4779-e29b-439e-a6a5-88c59229a6c7, 0250522c-6a1a-4f90-95dd-e85d1ea80014| 68cd85cc-e0b3-43c8-ba3c-67686dbf8a67 = 3472fe35-3fb2-40b8-bb38-1595bc82fab9, 58992513-6df4-450f-8b14-565ee2ea93ef| 9329c02a-4050-4798-93ae-b6e37b19af6d = 43957aaa-9863-4d9e-bf14-eb296d70676c, ca8db99c-7ec7-472f-a992-9934e8ad1b6a| 36da45f1-dd2c-4d1f-af13-5abe46b99921 = 819df3f9-e095-460e-8fe4-d48ba52e6833, eaf8a2cf-ec52-45a0-9129-b5f69d512fa8| 2f57b6c4-17e4-4965-ac1a-85ccccbe6c4a = 10dc0e5c-a18b-42b8-822e-e4925942d1fa, 641e6a20-84ba-4856-95ea-d4fd0e6b4ff6, 802a426c-a059-438d-b901-92d0f68129c3, 8f5ffda7-9633-4302-95fe-269922efe746, a7c1305c-d75b-4456-a59d-6860054e67f3, a8b9a07d-0bdd-408a-8539-df9a7771b974, ab359274-7fb9-4e21-b9b0-65d4d17977d6, d7094ba6-1d9c-4861-aafb-b8924d6719c5, e2bf6c94-808b-4529-8a8a-d0da2fadcffb, f6785a02-9f2f-44fc-9556-e31872590110, fac1a928-ac8c-4cb9-b83d-f57f840d9e9a| 72f988bf-86f1-41af-91ab-2d7cd011db47 = d25d9012-707a-4c30-8acb-414bf7650d69, c0d678c5-3792-47c7-b596-ab9b3bb58362, 0b9c2b4a-bc61-440d-b866-0e0ee4cdf70b, b6dfceeb-9df0-449c-94ec-e826a0f861bc, 49f00e3c-f677-487e-a5db-1025a508c72b, d0a3116c-66da-4e72-a8f4-16a69487758d, c27deb86-efbe-4e1b-a30b-c89e2504c2cb, 7d828bc4-a5f1-43b3-a7d2-2a54b6333700, 33325c7f-089d-493b-9ad9-1400a8da5394, 28fb5408-9ef4-4cd0-aa36-615450f4c5c6, cec93ece-eadc-4523-a992-35a331647abe, 02d59989-f8a9-4b69-9919-1ef51df4eff6, 14d16a2a-56f6-4c75-b091-084df9640297, 38304e13-357e-405e-9e9a-220351dcce8c, 42195872-7e70-4f8a-837f-84b28ecbb78b, 495944b2-66b7-4173-8824-77043bb269be, 509099b2-9d2c-4636-b43e-bd5cafb6be69, 5288acd1-ba79-4377-9205-9f220331a44a, 605e5f88-99d5-4be1-965d-445852415039, 62b829ee-7936-40c9-a1c9-47a93f9f3965, 6c48fa17-39c7-45f1-90ac-47a587128ace, 6d875e77-e412-4d7d-9af4-8895278b4443, 70f9301a-0dea-4b54-8e9f-6d3d26ca9376, 7bcb9a44-e0d3-4fe1-94ce-61c22e8091a5, 7bd905dc-801c-426a-9ead-8cb69b635b29, 7c943c1b-5122-4097-90c8-861411bdd574, 7e904d88-da43-429d-a048-dff4051b211f, 8ef99e6f-2766-45bd-a9e0-6c5e1aa70ff1, b364ed8d-4279-4bf8-8fd1-56f8fa0ae05c, b66c826e-98c4-4e29-b029-26d26d7d1f33, c183865e-6077-46f2-a3b1-deb0f4f4650a, ccd78886-4365-42e0-947d-8a976413ab5c, d659f9cd-9e4e-499e-ad75-8b45c97fdaba, d90d145a-4cdd-45a3-b2c4-971d69775278, da364f0f-307b-41c9-9d47-b7413ec45535, e80eb9fa-c996-4435-aa32-5af6f3d3077c, f0c630e0-2995-4853-b056-0b3c09cb673f, f2edfd5d-5496-4683-b94f-b3588c579009, f75d8d8b-6735-4697-82e1-1a7a3ff0d5d4, 735c31b9-0433-400c-b3a3-8d6e648d88cd, 1bc2cf76-8ae4-468b-84b0-b796a3ef0940; excludedsubscriptions:; excludedresourcetypes:; messageretrycutoffcount: 12");
            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            string[] fullincludedTenants = new string[] { "0598535e-625d-4d43-86bb-5240e2e61ee0", "5aea9a56-fb63-4da0-9bcb-33181cc1a75f", "6c5d07dd-5d1c-46fe-b5ba-11b422ebb6a0" };
            foreach (string t in fullincludedTenants)
            {
                var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                    tenantId: t));

                Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
                Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
            }

            string[] notIncludedTenants = new string[] { "af28624f-a59a-4b69-bb4b-b884ee871baa", "5a77e02b-7e11-4f7e-86bf-aee3222320e6" };
            foreach (string t in notIncludedTenants)
            {
                var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                    tenantId: t));

                Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
                Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
            }

            Dictionary<string, string> partialIncludedSubscriptions = new Dictionary<string, string>
            {
                { "72f988bf-86f1-41af-91ab-2d7cd011db47", "d90d145a-4cdd-45a3-b2c4-971d69775278" },
                { "9329c02a-4050-4798-93ae-b6e37b19af6d", "ca8db99c-7ec7-472f-a992-9934e8ad1b6a" }
            };
            foreach (var d in partialIncludedSubscriptions)
            {
                var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                    tenantId: d.Key,
                    subscriptionId: d.Value));

                Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
                Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
            }

            Dictionary<string, string> notincludedsubscriptions = new Dictionary<string, string>
            {
                { "72f988bf-86f1-41af-91ab-2d7cd011db47", "12cc4156-039c-4f4f-89ee-4e05fdc32fc1" },
                { "9329c02a-4050-4798-93ae-b6e37b19af6d", "0105420d-e42a-47f7-89a0-3e5c948c7f5d" }
            };
            foreach (var d in notincludedsubscriptions)
            {
                var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                    tenantId: d.Key,
                    subscriptionId: d.Value));

                Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
                Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
            }
        }

        [TestMethod]
        public void Test_ParseTrafficTunerConfig_Canary()
        {
            SetupConfig("allowalltenants: false; stopalltenants: false; includedsubscriptions:9329c02a-4050-4798-93ae-b6e37b19af6d = ca8db99c-7ec7-472f-a992-9934e8ad1b6a, 43957aaa-9863-4d9e-bf14-eb296d70676c|72f988bf-86f1-41af-91ab-2d7cd011db47 = 1a2311d9-66f5-47d3-a9fb-7a37da63934b, 62b829ee-7936-40c9-a1c9-47a93f9f3965, 7c943c1b-5122-4097-90c8-861411bdd574, b364ed8d-4279-4bf8-8fd1-56f8fa0ae05c; excludedsubscriptions:; excludedresourcetypes:; messageretrycutoffcount: 12");
            var tuner = new TrafficTuner(TrafficTunerRuleKey);
        }

        [TestMethod]
        public void Test_ParseTrafficTunerConfig_Int()
        {
            SetupConfig("allowalltenants: false; stopalltenants: false; includedsubscriptions: 72f988bf-86f1-41af-91ab-2d7cd011db47 = 1a2311d9-66f5-47d3-a9fb-7a37da63934b, 62b829ee-7936-40c9-a1c9-47a93f9f3965, 7c943c1b-5122-4097-90c8-861411bdd574, b364ed8d-4279-4bf8-8fd1-56f8fa0ae05c; excludedsubscriptions:; excludedresourcetypes:; messageretrycutoffcount: 12");
            var tuner = new TrafficTuner(TrafficTunerRuleKey);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_ExcludedResourceType()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription1},{subscription2};" +
                $"messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant,
                subscriptionId: subscription1,
                resourceType: "a",
                messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.ResourceType, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_NotExcludedResourceType()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription1},{subscription2};" +
                $"messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1, resourceType: "c", messageRetryCount: 12));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionAllowedCustomerAllowed()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription1};" +
                $"includedregions: westus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1, resourceType: "c", messageRetryCount: 12, resourceLocation: "westus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionAllowedCustomerNotAllowed()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription2};" +
                $"includedregions: westus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1, resourceType: "c", messageRetryCount: 12, resourceLocation: "westus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionNotAllowedCustomerAllowed()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription1};" +
                $"includedregions: eastus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1, resourceType: "c", messageRetryCount: 12, resourceLocation: "westus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionNotAllowedCustomerNotAllowed()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription2};" +
                $"includedregions: eastus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1, resourceType: "c", messageRetryCount: 12, resourceLocation: "westus"));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionNotSpecifiedInRequest()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription2};" +
                $"includedregions: eastus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest());

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionAllowedWithExcludedSubscriptions()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"excludedsubscriptions: {tenant}={subscription2};" +
                $"includedregions: eastus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant,
                subscriptionId: subscription2,
                resourceLocation: "eastus"));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.SubscriptionId, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceLocation: "eastus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionAllowlisted()
        {
            SetupConfig("includedregions: eastus");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(resourceLocation: "eastus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_RegionCombinations()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            string subscription2 = Guid.NewGuid().ToString();

            SetupConfig($"includedsubscriptions: {tenant}={subscription2}; excludedsubscriptions: {tenant}={subscription1};" +
                $"includedregions: westus;messageretrycutoffcount: 12;excludedResourceTypes: A,B");

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription1));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.SubscriptionId, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant, subscriptionId: subscription2));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceLocation: "westus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);

            result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceLocation: "eastus"));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsAllowed()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  }
            ];
            """;

            SetupConfig(config);

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceId: "/subscriptions/c9b3427c-a364-4619-a56b-f0c23c521c3a/providers/Microsoft.Resources/subscriptionRegistrations/Microsoft.Compute",
                resourceType: "Microsoft.Resources/subscriptionRegistrations"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_NonIncludedResourceTypeNotAllowed()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  }
            ];
            """;

            SetupConfig(config);

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceId: "/subscriptions/ec931d2f-361e-4c6e-b574-42d27b6dba08/providers/Microsoft.Inventory/subscriptionInternalProperties/default",
                resourceType: "Microsoft.Resources/subscriptionInternalProperties",
                subscriptionId: "ec931d2f-361e-4c6e-b574-42d27b6dba08"));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.Region, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsMultipleValuesAllowed()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  },
                  {
                      "resourceType": "Microsoft.Resources/subscriptionInternalProperties",
                      "matchValues" : [ "microsoft.compute" ],
                      "matchFunction": "Contains"
                  }
            ];
            """;

            SetupConfig(config);

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceId: "/subscriptions/c9b3427c-a364-4619-a56b-f0c23c521c3a/providers/Microsoft.Resources/subscriptionRegistrations/Microsoft.Compute",
                resourceType: "Microsoft.Resources/subscriptionRegistrations"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsNotAllowed()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  }
            ];
            """;

            SetupConfig(config);
            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceId: "/subscriptions/c9b3427c-a364-4619-a56b-f0c23c521c3a/providers/Microsoft.Resources/subscriptionRegistrations/Microsoft.Sql",
                resourceType: "Microsoft.Resources/subscriptionRegistrations"));

            Assert.AreEqual(TrafficTunerResult.NotAllowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.ResourceId, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsGlobalResourceIsAllowed()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  }
            ];
            """;

            SetupConfig(config);
            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                resourceId: "/providers/Microsoft.Inventory/skuProviders/Microsoft.Compute/resourceTypes/virtualMachines/locations/australiaeast/globalSkus/AZAP_Harvest_Compute_2",
                resourceType: "Microsoft.Inventory/skuProviders/resourceTypes/locations/globalskus"));

            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
            Assert.AreEqual(TrafficTunerNotAllowedReason.None, result.reason);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsAndSubscriptionsAllowed()
        {
            string tenant = Guid.NewGuid().ToString();
            string subscription1 = Guid.NewGuid().ToString();
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ],
                      "matchFunction": "EndsWith"
                  }
            ];
            """;

            SetupConfig($"includedsubscriptions: {tenant}={subscription1};" +
                config);

            var tuner = new TrafficTuner(TrafficTunerRuleKey);

            var result = tuner.EvaluateTunerResult(new TrafficTunerRequest(
                tenantId: tenant,
                subscriptionId: subscription1,
                resourceId: "/subscriptions/30033233-7071-43b7-abd4-dee283630133/providers/Microsoft.Resources/subscriptionZoneMappings/default",
                resourceType: "Microsoft.Resources/subscriptionZoneMappings"));
            Assert.AreEqual(TrafficTunerResult.Allowed, result.result);
        }

        [TestMethod]
        public void TestEvaluateTunerResult_IncludeResourceIdsMissingFieldThrowsException()
        {
            var config = """
                    includedResourceTypeWithMatchFunction: [
                  {
                      "resourceType": "Microsoft.Resources/subscriptionRegistrations",
                      "matchValues" : [ "microsoft.compute", "microsoft.network" ]
                  }
            ];
            """;

            SetupConfig(config);
            Assert.ThrowsException<InvalidOperationException>(() => new TrafficTuner(TrafficTunerRuleKey));
        }

        private static void SetupConfig(string mappings)
        {
            var appSettingsStub = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(mappings))
            {
                appSettingsStub.Add(TrafficTunerRuleKey, mappings);
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(appSettingsStub);

            ConfigMapUtil.Initialize(config, false);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigMapUtil.Reset();
        }
    }
}