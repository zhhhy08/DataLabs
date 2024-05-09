namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [TestClass]
    public class ArmUtilsTests
    {
        /*
        [DataTestMethod]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
            "{resourceProviderNamespace}/{typeName1}/{typeName2}",
            DisplayName = "TestGetObjectType_MultipleTypeNames1")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/Providers/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
            "{resourceProviderNamespace}/{typeName1}/{typeName2}",
            DisplayName = "TestGetObjectType_MultipleTypeNames2")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/PROVIDERS/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
            "{resourceProviderNamespace}/{typeName1}/{typeName2}",
            DisplayName = "TestGetObjectType_MultipleTypeNames3")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
            "{resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}",
            DisplayName = "TestGetObjectType_MultipleProviders1")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/Providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
            "{resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}",
            DisplayName = "TestGetObjectType_MultipleProviders2")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/Providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
            "{resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}",
            DisplayName = "TestGetObjectType_MultipleProviders3")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/PROVIDERS/{resourceProviderNamespace2}/{typeName3}/{name3}",
            "{resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}",
            DisplayName = "TestGetObjectType_MultipleProviders4")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/PROVIDERS/{resourceProviderNamespace2}/{typeName3}/Disk Read Bytes/sec",
            "{resourceProviderNamespace1}/{typeName1}/{typeName2}/{resourceProviderNamespace2}/{typeName3}",
            DisplayName = "TestGetObjectType_LastThreePiecesName")]
        [DataRow("/subscriptions/{subscriptionId}",
            "Microsoft.Resources/subscriptions",
            DisplayName = "TestGetObjectType_GetSubscription")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
            "Microsoft.Resources/subscriptions/resourcegroups",
            DisplayName = "TestGetObjectType_GetResourceGroup")]
        // Special case when there are 3 pieces left
        //"id": "/subscriptions/5feb0c29-4f0d-4018-8e54-7b89df699c15/resourceGroups/SvcTelemetry/providers/Microsoft.ClassicCompute/virtualMachines/SvcTelemetry/metricdefinitions/Disk Read Bytes/sec"
        // In above example "Disk Read Bytes/sec" should not have been split
        public void TestGetObjectType(
            string passedResourceType,
            string expectedParsedResourceType)
        {
            var result = ArmUtils.GetFullResourceType(
                passedResourceType,
                PartnerName.abcsolution.ToString());

            Assert.AreEqual(expectedParsedResourceType.ToLowerInvariant(), result);
        }
        */

        [DataTestMethod]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
           "{resourceProviderNamespace}/{typeName1}/{typeName2}",
           DisplayName = "TestGetObjectType_MultipleTypeNames1")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/Providers/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
           "{resourceProviderNamespace}/{typeName1}/{typeName2}",
           DisplayName = "TestGetObjectType_MultipleTypeNames2")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/PROVIDERS/{resourceProviderNamespace}/{typeName1}/{name1}/{typeName2}/{name2}",
           "{resourceProviderNamespace}/{typeName1}/{typeName2}",
           DisplayName = "TestGetObjectType_MultipleTypeNames3")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
           "{resourceProviderNamespace2}/{typeName3}",
           DisplayName = "TestGetObjectType_MultipleProviders1")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/Providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
           "{resourceProviderNamespace2}/{typeName3}",
           DisplayName = "TestGetObjectType_MultipleProviders2")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/Providers/{resourceProviderNamespace2}/{typeName3}/{name3}",
           "{resourceProviderNamespace2}/{typeName3}",
           DisplayName = "TestGetObjectType_MultipleProviders3")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/PROVIDERS/{resourceProviderNamespace2}/{typeName3}/{name3}",
           "{resourceProviderNamespace2}/{typeName3}",
           DisplayName = "TestGetObjectType_MultipleProviders4")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace1}/{typeName1}/{name1}/{typeName2}/{name2}/PROVIDERS/{resourceProviderNamespace2}/{typeName3}/Disk Read Bytes/sec",
           "{resourceProviderNamespace2}/{typeName3}",
           DisplayName = "TestGetObjectType_LastThreePiecesName")]
        [DataRow("/subscriptions/{subscriptionId}",
           "Microsoft.Resources/subscriptions",
           DisplayName = "TestGetObjectType_GetSubscription")]
        [DataRow("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
           "Microsoft.Resources/subscriptions/resourcegroups",
           DisplayName = "TestGetObjectType_GetResourceGroup")]
        [DataRow("/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/argfeaturedatapoc",
           "Microsoft.Features/featureProviders/subscriptionFeatureRegistrations",
           DisplayName = "TestGetObjectType_AfecType")]
        // Special case when there are 3 pieces left
        //"id": "/subscriptions/5feb0c29-4f0d-4018-8e54-7b89df699c15/resourceGroups/SvcTelemetry/providers/Microsoft.ClassicCompute/virtualMachines/SvcTelemetry/metricdefinitions/Disk Read Bytes/sec"
        // In above example "Disk Read Bytes/sec" should not have been split
        public void TestGetResourceType(
           string passedResourceType,
           string expectedParsedResourceType)
        {
            var result = ArmUtils.GetResourceType(passedResourceType);
            Assert.AreEqual(expectedParsedResourceType.ToLowerInvariant(), result);
        }

        [DataTestMethod]
        [DataRow("{resourceProviderNamespace}/{typeName1}/{typeName2}/{action}",
           "{action}",
           DisplayName = "TestGetAction_Action")]
        [DataRow("{resourceProviderNamespace}/{typeName1}/{typeName2}/{action}/",
           "{action}",
           DisplayName = "TestGetAction_ActionEndingSlash")]
        [DataRow("{type}/{action}",
           "{action}",
           DisplayName = "TestGetAction_SimpleTypeAndAction")]
        [DataRow("{type}/{action}/",
           "{action}",
           DisplayName = "TestGetAction_SimpleTypeAndActionEndingSlash")]
        [DataRow("{action}",
           "{action}",
           DisplayName = "TestGetAction_ActionOnly")]
        [DataRow("/{action}/",
           "{action}",
           DisplayName = "TestGetAction_ActionOnlyEndingSlash1")]
        [DataRow("{action}/",
           "{action}",
           DisplayName = "TestGetAction_ActionOnlyEndingSlash2")]
        [DataRow("move/action",
           "move/action",
           DisplayName = "TestGetAction_ActionMoveAlternative")]
        [DataRow("/move/action",
           "move/action",
           DisplayName = "TestGetAction_SlashedActionMoveAlternative")]
        [DataRow("register/action",
           "register/action",
           DisplayName = "TestGetAction_ActionSubscription")]
        [DataRow("ActionSubscriptionChange",
           "ActionSubscriptionChange",
           DisplayName = "TestGetAction_ActionSubscriptionChange")]
        [DataRow("elevateaccess/action",
           "elevateaccess/action",
           DisplayName = "TestGetAction_ActionElevateAccess")]
        [DataRow("unregistered",
           "unregistered",
           DisplayName = "TestGetAction_ActionUnregistered")]
        [DataRow("registered",
           "registered",
           DisplayName = "TestGetAction_ActionRegistered")]
        [DataRow("deleted",
           "deleted",
           DisplayName = "TestGetAction_ActionDeleted")]
        [DataRow("/",
           null,
           DisplayName = "TestGetAction_ActionNull")]
        [DataRow("///",
           null,
           DisplayName = "TestGetAction_ActionNull2")]
        public void GetAction(
           string passedEventType,
           string expectedParsedAction)
        {
            var result = ArmUtils.GetAction(passedEventType);
            Assert.AreEqual(expectedParsedAction?.ToLowerInvariant(), result);
        }

        [DataTestMethod]
        [DataRow("/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations",
           "Microsoft.Features/featureProviders/subscriptionFeatureRegistrations",
           DisplayName = "TestGetObjectType_AfecType")]
        [DataRow("/subscriptions/eaab1166-1e13-4370-a951-6ed345a48c15/providers/Microsoft.Features/featureProviders/Microsoft.Compute/subscriptionFeatureRegistrations/",
           "Microsoft.Features/featureProviders/subscriptionFeatureRegistrations",
           DisplayName = "TestGetObjectType_AfecType")]
        public void TestGetResourceTypeForCollectionCall(
            string passedResourceType,
            string expectedParsedResourceType)
        {
            var result = ArmUtils.GetResourceTypeForCollectionCall(passedResourceType);
            Assert.AreEqual(expectedParsedResourceType.ToLowerInvariant(), result);
        }
    }
}
