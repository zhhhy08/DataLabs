namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;

    [TestClass]
    public class ResourceIdExtensionsTest
    {
        private const string subLevelProxyId =
            "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/providers/Microsoft.authorization/Classicadministrators/Admin1";
        private const string sampleIdWithNonStandardCasing = "/subscriptions/0b88dfdb-55B3-4fb0-b474-5b6dcbe6b2EF/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";
        private const string subscriptionPrefix = "/subscriptions";
        private const string globalScope = "/";
        private const string IdWithoutResourceGroup = "/subscriptions/0b88dfdb-55B3-4fb0-b474-5b6dcbe6b2EF/resourceGroups/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";

        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        #region Subscription Level Proxy Resources

        [TestMethod]
        public void ValidateSubLevelProxyId()
        {
            Assert.IsTrue(subLevelProxyId.IsValidResourceId());
        }

        [TestMethod]
        public void TestGetSubscriptionInSubLevelProxyId()
        {
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", subLevelProxyId.GetSubscriptionId());
        }

        [TestMethod]
        public void TestGetResourceTypeInSubLevelProxyId()
        {
            Assert.AreEqual("Microsoft.authorization/Classicadministrators", subLevelProxyId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeInManagementGroup()
        {
            Assert.AreEqual("Microsoft.Management/managementgroups", ResourcesConstants.ManagementGroupResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceNamespaceInSubLevelProxyId()
        {
            Assert.AreEqual("Microsoft.authorization", subLevelProxyId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNameInSubLevelProxyId()
        {
            Assert.AreEqual("Admin1", subLevelProxyId.GetResourceName());
        }

        [TestMethod]
        public void TestGetResourceGroupInSubLevelProxyId()
        {
            Assert.IsNull(subLevelProxyId.GetResourceGroup());
            Assert.IsNull(subLevelProxyId.FastGetResourceGroup());
            Assert.IsNull(subLevelProxyId.GetNormalizedResourceGroup());
        }

        [TestMethod]
        public void TestGetResourceGroupInManagementGroup()
        {
            Assert.IsNull(ResourcesConstants.ManagementGroupResourceId.GetResourceGroup());
            Assert.IsNull(ResourcesConstants.ManagementGroupResourceId.FastGetResourceGroup());
            Assert.IsNull(ResourcesConstants.MgRoleAssignmentResourceId.GetNormalizedResourceGroup());
        }

        #endregion

        [TestMethod]
        public void ValidateResourceId()
        {
            var invalidId = "/invalidString/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";
            var managementGroupNestedResource = "/providers/Microsoft.Management/managementGroups/AzBlueprintRunners/providers/Microsoft.Blueprint/blueprints/AssignBlueprint";
            var spotInstanceResource = "/providers/Microsoft.Compute/skuSpotEvictionRate/Standard_D2_v2/location/eastus";
            Assert.IsTrue(ResourcesConstants.SampleResourceId.IsValidResourceId());
            Assert.IsTrue(ResourcesConstants.NestedResourceId.IsValidResourceId());
            Assert.IsTrue(ResourcesConstants.MgRoleAssignmentResourceId.IsValidResourceId());
            Assert.IsTrue(ResourcesConstants.GlobalRoleAssignmentResourceId.IsValidResourceId());
            Assert.IsTrue(ResourcesConstants.ManagementGroupResourceId.IsValidResourceId());
            Assert.IsTrue(ResourcesConstants.ResourceGroupResourceId.IsValidResourceId());
            Assert.IsTrue(managementGroupNestedResource.IsValidResourceId());
            Assert.IsTrue(spotInstanceResource.IsValidResourceId());

            Assert.IsFalse(invalidId.IsValidResourceId());
            Assert.IsFalse(IdWithoutResourceGroup.IsValidResourceId());
        }

        [TestMethod]
        public void ValidateSubscriptionResource()
        {
            Assert.IsTrue("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef".IsValidResourceId());
        }

        [TestMethod]
        public void ValidateInvalidSubscriptionResource()
        {
            Assert.IsFalse("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2e".IsValidResourceId());
        }

        [TestMethod]
        public void TestGetResourceType()
        {
            Assert.AreEqual("Microsoft.ClassicStorage/storageAccounts", ResourcesConstants.SampleResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnExtensionResource()
        {
            Assert.AreEqual("Microsoft.Advisor/recommendations", ResourcesConstants.ExtensionResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeAndNameOnExtensionResource()
        {
            var resourceTypeAndName = ResourcesConstants.ExtensionResourceId.GetResourceTypeAndName();
            Assert.AreEqual("Microsoft.Advisor/recommendations", resourceTypeAndName.resourceType);
            Assert.AreEqual("973d2fe1-7452-8449-3c5d-f8b41b4b54ea", resourceTypeAndName.resourceName);
        }

        [TestMethod]
        public void TestGetResourceTypeOnSubscriptionRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization/roleAssignments", ResourcesConstants.SubscriptionRoleAssignmentResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnResourceRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization/roleAssignments", ResourcesConstants.ResourceRoleAssignmentResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnManagementGroupRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization/roleAssignments", ResourcesConstants.MgRoleAssignmentResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnGlobalRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization/roleAssignments", ResourcesConstants.GlobalRoleAssignmentResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnTenantTypeRoleAssignment()
        {
            Assert.AreEqual("NotSupportedRoleAssignments", ResourcesConstants.GlobalNotSupportedRoleAssignmentResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnSubscription()
        {
            Assert.AreEqual("Microsoft.Resources/subscriptions", ResourcesConstants.SubscriptionResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnResourceGroup()
        {
            Assert.AreEqual("Microsoft.Resources/subscriptions/resourcegroups", ResourcesConstants.ResourceGroupResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceTypeOnLocations()
        {
            Assert.AreEqual("Microsoft.Resources/locations", ResourcesConstants.LocationResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnSubscription()
        {
            Assert.AreEqual("Microsoft.Resources", ResourcesConstants.SubscriptionResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnResourceGroup()
        {
            Assert.AreEqual("Microsoft.Resources", ResourcesConstants.ResourceGroupResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnManagementGroup()
        {
            Assert.AreEqual("Microsoft.Management", ResourcesConstants.ManagementGroupResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetNestedResourceType()
        {
            Assert.AreEqual("Microsoft.ClassicStorage/vmscaleset/virtualmachine", ResourcesConstants.NestedResourceId.GetResourceType());
        }

        [TestMethod]
        public void TestGetResourceNamespace()
        {
            Assert.AreEqual("Microsoft.ClassicStorage", ResourcesConstants.SampleResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnExtensionResource()
        {
            Assert.AreEqual("Microsoft.Advisor", ResourcesConstants.ExtensionResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnManagementGroupRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization", ResourcesConstants.MgRoleAssignmentResourceId.GetResourceNamespace());
        }

        [TestMethod]
        public void TestGetResourceNamespaceOnGlobalRoleAssignment()
        {
            Assert.AreEqual("Microsoft.Authorization", ResourcesConstants.GlobalRoleAssignmentResourceId.GetResourceNamespace());
        }

        [DataRow(ResourcesConstants.SampleResourceId, "Microsoft.ClassicStorage", "storageAccounts")]
        [DataRow(ResourcesConstants.NestedResourceId, "Microsoft.ClassicStorage", "vmscaleset/virtualmachine")]
        [DataRow(ResourcesConstants.ResourceGroupResourceId, "Microsoft.Resources", "subscriptions/resourcegroups")]
        [DataRow(ResourcesConstants.SubscriptionResourceId, "Microsoft.Resources", "subscriptions")]
        [DataRow(ResourcesConstants.ExtensionResourceId, "Microsoft.Advisor", "recommendations")]
        [DataRow(ResourcesConstants.ManagementGroupResourceId, "Microsoft.Management", "managementgroups")]
        [TestMethod]
        public void TestGetNestedResourceNamespace(string resourceId, string expectedNamespace, string expectedType)
        {
            var res = resourceId.GetResourceNamespaceAndType();
            Assert.AreEqual(expectedNamespace, res.Item1);
            Assert.AreEqual(expectedType, res.Item2);
        }

        [TestMethod]
        public void TestGetNestedResourceNamespace()
        {
            Assert.AreEqual("Microsoft.ClassicStorage", ResourcesConstants.NestedResourceId.GetResourceNamespaceAndType().providerName);
        }

        [TestMethod]
        public void TestGetNestedResourceNamespaceAndType()
        {
            Assert.AreEqual("Microsoft.ClassicStorage", ResourceIdExtensions.SplitResourceNamespaceAndTypeFromFullType(ResourcesConstants.SampleResourceType).Item1);
            Assert.AreEqual("storageAccounts", ResourceIdExtensions.SplitResourceNamespaceAndTypeFromFullType(ResourcesConstants.SampleResourceType).Item2);
            Assert.AreEqual("Microsoft.ClassicStorage", ResourceIdExtensions.SplitResourceNamespaceAndTypeFromFullType(ResourcesConstants.NestedResourceType).Item1);
            Assert.AreEqual("vmscaleset/virtualmachine", ResourceIdExtensions.SplitResourceNamespaceAndTypeFromFullType(ResourcesConstants.NestedResourceType).Item2);
        }

        [TestMethod]
        public void TestIsMicrosoftResourcesProviderSubscriptionType()
        {
            Assert.IsTrue(ResourceIdExtensions.IsMicrosoftResourcesProviderSubscriptionType("Microsoft.Resources/subscriptions"));
        }

        [TestMethod]
        public void TestGetSubscriptionId()
        {
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", ResourcesConstants.SampleResourceId.GetSubscriptionId());
        }

        [TestMethod]
        public void TestSafeGetSubscriptionId()
        {
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", ResourcesConstants.SampleResourceId.GetSubscriptionIdOrNull());
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", ResourcesConstants.ExtensionResourceId.GetSubscriptionIdOrNull());
            Assert.AreEqual("2838bbb1-d577-4b16-bb6d-be2236b59cf5", "/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5".GetSubscriptionIdOrNull());
            Assert.AreEqual("0b88dfdb55b34fb0b4745b6dcbe6b2ef", "/subscriptions/0b88dfdb55b34fb0b4745b6dcbe6b2ef".GetSubscriptionIdOrNull());
            Assert.IsNull(subscriptionPrefix.GetSubscriptionIdOrNull());
            Assert.IsNull(globalScope.GetSubscriptionIdOrNull());
            Assert.IsNull(ResourcesConstants.ManagementGroupResourceId.GetSubscriptionIdOrNull());
        }

        [TestMethod]
        public void TestGetResourceGroup()
        {
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.GetResourceGroup());
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.FastGetResourceGroup());
        }

        [TestMethod]
        public void TestGetResourceGroupInMalformattedId()
        {
            var resourceId =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups//Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";

            Assert.AreEqual("Default-Storage-EastUS", resourceId.GetResourceGroup());
            Assert.AreEqual("Default-Storage-EastUS", resourceId.FastGetResourceGroup());
        }

        [TestMethod]
        public void TestGetResourceGroupInInvalidId()
        {
            // Technically, this id is invalid; however, the resourceGroup retrieval shoud still be correct even though
            // there are 2 "resourceGroups" sections
            var resourceId = "/subscriptions/resourceGroups/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics";
            Assert.AreEqual("Default-Storage-EastUS", resourceId.GetResourceGroup());
            Assert.AreEqual("Default-Storage-EastUS", resourceId.FastGetResourceGroup());

            // Even though this id has "resourceGroups", it is not a key to a segment but rather a value. Thus the return value should be null.
            resourceId = "/providers/microsoft.managment/managementGroups/resourceGroups/providers/Microsoft.Authorization/PolicyAssignments/Pa1";
            Assert.IsNull(resourceId.GetResourceGroup());
            Assert.IsNull(resourceId.FastGetResourceGroup());
        }

        [TestMethod]
        public void TestGetNormalizedResourceGroup()
        {
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.GetResourceGroup());
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.FastGetResourceGroup());
            Assert.AreEqual("default-storage-eastus", ResourcesConstants.SampleResourceId.GetNormalizedResourceGroup());
        }

        [TestMethod]
        public void TestGetResourceName()
        {
            Assert.AreEqual("aainteusanalytics", ResourcesConstants.SampleResourceId.GetResourceName());
            Assert.AreEqual("vm1", ResourcesConstants.NestedResourceId.GetResourceName());
            Assert.AreEqual("973d2fe1-7452-8449-3c5d-f8b41b4b54ea", ResourcesConstants.ExtensionResourceId.GetResourceName());
            Assert.AreEqual("aainteusanalytics", ResourcesConstants.SampleResourceId.GetResourceName());
            Assert.IsNull("aainteusanalytics".GetResourceName());
        }

        [TestMethod]
        public void TestGetResourceFullName()
        {
            Assert.AreEqual("aainteusanalytics", ResourcesConstants.SampleResourceId.GetResourceFullName());
            Assert.AreEqual("aainteusanalytics/vm1", ResourcesConstants.NestedResourceId.GetResourceFullName());
            Assert.AreEqual("973d2fe1-7452-8449-3c5d-f8b41b4b54ea", ResourcesConstants.ExtensionResourceId.GetResourceFullName());
            Assert.AreEqual("mg1", ResourcesConstants.ManagementGroupResourceId.GetResourceFullName());
        }

        [TestMethod]
        public void TestGetParentNamePaths()
        {
            var testCases = new Dictionary<string, List<string>>
            {
                {
                    ResourcesConstants.SampleResourceId,
                    new List<string>()
                },
                {
                    ResourcesConstants.NestedResourceId,
                    new List<string> { "aainteusanalytics" }
                },
                {
                    ResourcesConstants.MultiLevelNestedResourceId,
                    new List<string> { "aainteusanalytics", "vm1" }
                },
                {
                    ResourcesConstants.ExtensionResourceId,
                    new List<string> { "tnzn4h7oyb" }
                },
                {
                    "INVALID_ID",
                    new List<string>()
                },
                {
                    ResourcesConstants.SubscriptionResourceId,
                    new List<string>()
                }
            };

            foreach (var testCase in testCases)
            {
                var paths = testCase.Key.GetParentNamePaths();

                Assert.IsNotNull(paths, $"Expecting non-null parent name path from resource id ${testCase.Key}");
                Assert.AreEqual(testCase.Value.Count, paths.Count, $"Parent name paths length for ${testCase.Key} is incorrect.");
                for (var i = 0; i < paths.Count; i++)
                {
                    Assert.AreEqual(testCase.Value[i], paths[i], $"Parent name path at index {i} is not as expected.");
                }
            }
        }

        [TestMethod]
        public void TestGetParentResourceName()
        {
            Assert.IsNull(ResourcesConstants.SampleResourceId.GetParentResourceName());
            Assert.AreEqual("aainteusanalytics", ResourcesConstants.NestedResourceId.GetParentResourceName());
        }

        [TestMethod]
        public void TestGetProviderAndTypesAndNamesPaths()
        {
            var testCases = new Dictionary<string, List<(string provider, List<(string type, string name)> typesAndNames)>>
            {
                {
                    ResourcesConstants.SampleResourceId,
                    new List<(string, List<(string, string)>)>
                    {
                        (
                        ResourcesConstants.SampleResourceId.GetResourceNamespaceAndType().Item1,
                        new List<(string, string)>
                        {
                            ("storageAccounts", "aainteusanalytics")
                        }
                        )
                    }

                },
                {
                    ResourcesConstants.NestedResourceId,
                    new List<(string, List<(string, string)>)>
                    {
                        (
                            ResourcesConstants.NestedResourceId.GetResourceNamespaceAndType().Item1,
                            new List<(string, string)>
                            {
                                ("vmscaleset", "aainteusanalytics"),
                                ("virtualmachine", "vm1")
                            }
                        )
                    }
                },
                {
                    ResourcesConstants.MultiLevelNestedResourceId,
                    new List<(string, List<(string, string)>)>
                    {
                        (
                            ResourcesConstants.MultiLevelNestedResourceId.GetResourceNamespaceAndType().Item1,
                            new List<(string, string)>
                            {
                                ("vmscaleset", "aainteusanalytics"),
                                ("virtualmachine", "vm1"),
                                ("extensions", "ext1")
                            }
                        )
                    }
                },
                {
                    ResourcesConstants.ExtensionResourceId,
                    new List<(string, List<(string, string)>)>
                    {
                        (
                            "Microsoft.Sql",
                            new List<(string, string)>
                            {
                                ("servers", "tnzn4h7oyb")
                            }
                        ),
                        (
                            "Microsoft.Advisor",
                            new List<(string, string)>
                            {
                                ("recommendations", "973d2fe1-7452-8449-3c5d-f8b41b4b54ea")
                            }
                        )
                    }
                },
                {
                    "INVALID_ID",
                    new List<(string, List<(string, string)>)>()
                },
                {
                    ResourcesConstants.SubscriptionResourceId,
                    new List<(string, List<(string, string)>)>()
                }
            };

            foreach (var testCase in testCases)
            {
                var paths = testCase.Key.GetProviderTypeNamePaths();

                Assert.IsNotNull(paths, $"Expecting non-null provider-type-name path from resource id ${testCase.Key}");
                Assert.AreEqual(testCase.Value.Count, paths.Count, $"Parent name paths length for ${testCase.Key} is incorrect.");
                for (var i = 0; i < paths.Count; i++)
                {
                    Assert.AreEqual(testCase.Value[i].provider, paths[i].provider, $"Parsed provider at index {i} is not as expected.");
                    Assert.AreEqual(testCase.Value[i].typesAndNames.Count, paths[i].typeNamePaths.Count, $"Number of type-name paths are not the same at index {i}.");

                    for (var j = 0; j < paths[i].typeNamePaths.Count; j++)
                    {
                        Assert.AreEqual(testCase.Value[i].typesAndNames[j].type, paths[i].typeNamePaths[j].type, $"Type path at index {i} path {j} is not as expected.");
                        Assert.AreEqual(testCase.Value[i].typesAndNames[j].name, paths[i].typeNamePaths[j].name, $"Name path at index {i} path {j} is not as expected.");
                    }
                }
            }
        }

        [TestMethod]
        public void TestGetScopesForSubscription()
        {
            var scopes = ResourcesConstants.SubscriptionResourceId.GetScopes();
            Assert.AreEqual(1, scopes.Count);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", scopes[0]);
        }

        [TestMethod]
        public void TestGetScopesForResourceGroup()
        {
            var scopes = ResourcesConstants.ResourceGroupResourceId.GetScopes();
            Assert.AreEqual(2, scopes.Count);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", scopes[0]);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-SQL-WestUS", scopes[1]);
        }

        [TestMethod]
        public void TestGetScopes()
        {
            var scopes = ResourcesConstants.NestedResourceId.GetScopes();
            Assert.AreEqual(4, scopes.Count);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", scopes[0]);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS", scopes[1]);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/vmscaleset/aainteusanalytics", scopes[2]);
            Assert.AreEqual(ResourcesConstants.NestedResourceId, scopes[3]);
        }

        [TestMethod]
        public void TestQuadNestedIdGetScopesWithSubscriptionInType()
        {
            var quadNestedId = "/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/subscriptions/d41d8cd98f00b204e9800998ecf8427e/rules/SubscriptionFilter";
            var scopes = quadNestedId.GetScopes();
            Assert.AreEqual(6, scopes.Count);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5", scopes[0]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging", scopes[1]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service", scopes[2]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production", scopes[3]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/subscriptions/d41d8cd98f00b204e9800998ecf8427e", scopes[4]);
            Assert.AreEqual(quadNestedId, scopes[5]);
        }

        [TestMethod]
        public void TestGetScopesManagementGroup()
        {
            var scopes = ResourcesConstants.ManagementGroupResourceId.GetScopes();
            Assert.AreEqual(1, scopes.Count);
            Assert.AreEqual(ResourcesConstants.ManagementGroupResourceId, scopes[0]);
        }

        [TestMethod]
        public void TestGetScopesManagementGroupRoleAssigment()
        {
            var scopes = ResourcesConstants.MgRoleAssignmentResourceId.GetScopes();
            Assert.AreEqual(2, scopes.Count);
            Assert.AreEqual("/providers/Microsoft.Management/managementGroups/ITAdmins", scopes[0]);
            Assert.AreEqual(ResourcesConstants.MgRoleAssignmentResourceId, scopes[1]);

        }

        [TestMethod]
        public void TestExtensionIdGetScopes()
        {
            var extensionId = "/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/providers/Microsoft.EventGrid/eventsubscriptions/Subscription1";
            var scopes = extensionId.GetScopes();
            Assert.AreEqual(5, scopes.Count);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5", scopes[0]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging", scopes[1]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service", scopes[2]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production", scopes[3]);
            Assert.AreEqual(extensionId, scopes[4]);
        }

        [TestMethod]
        public void TestGetNonContainerScopesForSubscription()
        {
            var scopes = ResourcesConstants.SubscriptionResourceId.GetNonContainerScopes();
            Assert.AreEqual(0, scopes.Count);
        }

        [TestMethod]
        public void TestGetNonContainerScopesForResourceGroup()
        {
            var scopes = ResourcesConstants.ResourceGroupResourceId.GetNonContainerScopes();
            Assert.AreEqual(0, scopes.Count);
        }

        [TestMethod]
        public void TestGetNonContainerScopes()
        {
            var scopes = ResourcesConstants.NestedResourceId.GetNonContainerScopes();
            Assert.AreEqual(2, scopes.Count);
            Assert.AreEqual("/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/vmscaleset/aainteusanalytics", scopes[0]);
            Assert.AreEqual(ResourcesConstants.NestedResourceId, scopes[1]);
        }

        [TestMethod]
        public void TestQuadNestedIdGetNonContainerScopesWithSubscriptionInType()
        {
            var quadNestedId = "/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/subscriptions/d41d8cd98f00b204e9800998ecf8427e/rules/SubscriptionFilter";
            var scopes = quadNestedId.GetNonContainerScopes();
            Assert.AreEqual(4, scopes.Count);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service", scopes[0]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production", scopes[1]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/subscriptions/d41d8cd98f00b204e9800998ecf8427e", scopes[2]);
            Assert.AreEqual(quadNestedId, scopes[3]);
        }

        [TestMethod]
        public void TestGetNonContainerScopesManagementGroup()
        {
            var scopes = ResourcesConstants.ManagementGroupResourceId.GetNonContainerScopes();
            Assert.AreEqual(0, scopes.Count);
        }

        [TestMethod]
        public void TestGetNonContainerScopesManagementGroupRoleAssigment()
        {
            var scopes = ResourcesConstants.MgRoleAssignmentResourceId.GetNonContainerScopes();
            Assert.AreEqual(1, scopes.Count);
            Assert.AreEqual(ResourcesConstants.MgRoleAssignmentResourceId, scopes[0]);
        }

        [TestMethod]
        public void TestExtensionIdGetNonContainerScopes()
        {
            var extensionId = "/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production/providers/Microsoft.EventGrid/eventsubscriptions/Subscription1";
            var scopes = extensionId.GetNonContainerScopes();
            Assert.AreEqual(3, scopes.Count);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service", scopes[0]);
            Assert.AreEqual("/subscriptions/2838bbb1-d577-4b16-bb6d-be2236b59cf5/resourceGroups/virteom-jobs-messaging/providers/Microsoft.ServiceBus/namespaces/virteom-jobs-service/topics/ServiceManagement-production", scopes[1]);
            Assert.AreEqual(extensionId, scopes[2]);
        }

        [TestMethod]
        public void TestSplitExtensionResourceId()
        {
            var sampleSplit = ResourcesConstants.SampleResourceId.SplitExtensionResourceId();
            Assert.AreEqual(1, sampleSplit.Count);
            Assert.AreEqual(ResourcesConstants.SampleResourceId, sampleSplit.First());

            var nestedSplit = ResourcesConstants.NestedResourceId.SplitExtensionResourceId();
            Assert.AreEqual(1, nestedSplit.Count);
            Assert.AreEqual(ResourcesConstants.NestedResourceId, nestedSplit.First());

            var extensionSplit = ResourcesConstants.ExtensionResourceId.SplitExtensionResourceId();
            var extensionProviderLastIndex = ResourcesConstants.ExtensionResourceId.LastIndexOf(ResourceIdExtensions.ProvidersSegment, StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(2, extensionSplit.Count);
            Assert.AreEqual(ResourcesConstants.ExtensionResourceId.Substring(0, extensionProviderLastIndex - 1), extensionSplit[0]);
            Assert.AreEqual(ResourcesConstants.ExtensionResourceId.Substring(extensionProviderLastIndex), extensionSplit[1]);
        }

        [TestMethod]
        public void TestSplittingWhenProvidersInResourceId()
        {
            var id =
                "/subscriptions/4dcc6b77-2840-4fe4-b12f-84ce184a3b56/resourcegroups/providers/providers/microsoft.classiccompute/domainnames/providers";
            var splitted = id.SplitExtensionResourceId();
            Assert.AreEqual(2, splitted.Count);
            Assert.AreEqual("/subscriptions/4dcc6b77-2840-4fe4-b12f-84ce184a3b56/resourcegroups/providers",
                splitted[0]);
            Assert.AreEqual("providers/microsoft.classiccompute/domainnames/providers", splitted[1]);

            Assert.AreEqual("providers", ResourceIdExtensions.GetResourceName(id));
            Assert.AreEqual("providers", ResourceIdExtensions.GetResourceGroup(id));
            Assert.AreEqual("providers", ResourceIdExtensions.FastGetResourceGroup(id));
            Assert.AreEqual("microsoft.classiccompute/domainnames", ResourceIdExtensions.GetResourceType(id));
        }

        [TestMethod]
        public void TestGetNormalizedSubscriptionId()
        {
            Assert.AreEqual("0b88dfdb-55B3-4fb0-b474-5b6dcbe6b2EF", sampleIdWithNonStandardCasing.GetSubscriptionId());
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", sampleIdWithNonStandardCasing.GetNormalizedSubscriptionId());
        }

        [TestMethod]
        public void TestGetManagementGroupName()
        {
            Assert.AreEqual("mg1", ResourcesConstants.ManagementGroupResourceId.GetManagementGroupName());
            Assert.IsNull("providers/not/management/group".GetManagementGroupName());
            Assert.AreEqual("ITAdmins", ResourcesConstants.MgRoleAssignmentResourceId.GetManagementGroupName());

            Assert.AreEqual("mg1", ResourcesConstants.ManagementGroupResourceId.FastGetManagementGroupName());
            Assert.IsNull("providers/not/management/group".FastGetManagementGroupName());
            Assert.AreEqual("ITAdmins", ResourcesConstants.MgRoleAssignmentResourceId.FastGetManagementGroupName());
        }

        /// <summary>
        /// We are splitting by word "provider" during parsing, so there are some case when we won't be able to parse.
        /// But we didn't see such examples so far.
        /// </summary>
        [TestMethod]
        public void TestKnownParsingLogicFragility()
        {
            // We saw such examples and are working correctly with them
            var resourceIdWithProvider =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/providers/providers/Microsoft.ClassicStorage/storageAccounts/providers";
            Assert.AreEqual("providers", resourceIdWithProvider.GetResourceName());
            Assert.AreEqual("providers", resourceIdWithProvider.GetResourceGroup());
            Assert.AreEqual("providers", resourceIdWithProvider.FastGetResourceGroup());

            // Works, but throws exception to logs. Such cases aren't expected for now.
            var nestedIdWithProvider =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/vmscaleset/providers/virtualmachine/vm1";
            Assert.AreEqual("Microsoft.ClassicStorage/vmscaleset/virtualmachine",
                nestedIdWithProvider.GetResourceType());

            // We do NOT parse correctly for examples below. But we did't see such ids

            // If resource name will be "{something}/provider" we won't work
            var resourceIdWrongName =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/providers/providers/Microsoft.ClassicStorage/storageAccounts/test/providers";
            Assert.AreEqual(null, resourceIdWrongName.GetResourceName());

            var resourceIdWithWrongResourceType =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/resourceGroupName/providers/Microsoft.ClassicStorage/providers/name";
            Assert.AreEqual("name", resourceIdWithWrongResourceType.GetResourceType());
        }

        [TestMethod]
        public void TestGetIdSegments()
        {
            var segments = ResourcesConstants.NestedResourceId.GetIdSegments().ToList();
            Assert.AreEqual(5, segments.Count);
            Assert.AreEqual("subscriptions", segments[0].Key);
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", segments[0].Value);
            Assert.AreEqual("resourceGroups", segments[1].Key);
            Assert.AreEqual("Default-Storage-EastUS", segments[1].Value);
            Assert.AreEqual("providers", segments[2].Key);
            Assert.AreEqual("Microsoft.ClassicStorage", segments[2].Value);
            Assert.AreEqual("vmscaleset", segments[3].Key);
            Assert.AreEqual("aainteusanalytics", segments[3].Value);
            Assert.AreEqual("virtualmachine", segments[4].Key);
            Assert.AreEqual("vm1", segments[4].Value);
        }

        [TestMethod]
        public void TestGetSubscriptionIdOnInvalidIds()
        {
            var exception = Assert.ThrowsException<ArgumentException>(() => "/".GetSubscriptionId());
            Assert.AreEqual("ResourceId / must be more than just slashes.", exception.Message);
            exception = Assert.ThrowsException<ArgumentException>(() => "//".GetSubscriptionId());
            Assert.AreEqual("ResourceId // must be more than just slashes.", exception.Message);
            exception = Assert.ThrowsException<ArgumentException>(() => "/subscriptions".GetSubscriptionId());
            Assert.AreEqual("Resource id /subscriptions must have slash after subscriptions segment.", exception.Message);
            exception = Assert.ThrowsException<ArgumentException>(() => "/subscriptions/".GetSubscriptionId());
            Assert.AreEqual("Resource id /subscriptions/ must have subscriptionId after subscriptions segment.", exception.Message);
            exception = Assert.ThrowsException<ArgumentException>(() => "/subscriptions//".GetSubscriptionId());
            Assert.AreEqual("Resource id /subscriptions// must have subscriptionId after subscriptions segment.", exception.Message);
        }

        [TestMethod]
        public void TestGetSubscriptionIdOnIrregularIds()
        {
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", "//subscriptions//0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef".GetSubscriptionId());
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", "subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef".GetSubscriptionId());
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef//".GetSubscriptionId());
        }

        [TestMethod]
        public void TestIsTenantLevelResource()
        {
            Assert.IsFalse(ResourcesConstants.SampleResourceId.IsTenantLevelResource());
            Assert.IsFalse(ResourcesConstants.NestedResourceId.IsTenantLevelResource());
            Assert.IsFalse(ResourcesConstants.SubscriptionResourceId.IsTenantLevelResource());
            // Some notification coming without "/" on the beginning
            Assert.IsFalse("subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aainteusanalytics".IsTenantLevelResource());

            Assert.IsTrue(ResourcesConstants.MgRoleAssignmentResourceId.IsTenantLevelResource());
            Assert.IsTrue(ResourcesConstants.GlobalRoleAssignmentResourceId.IsTenantLevelResource());
            Assert.IsTrue(ResourcesConstants.ManagementGroupResourceId.IsTenantLevelResource());
        }

        #region MG Resource Tests

        [TestMethod]
        public void Test_IsValidManagementGroupDeprecated()
        {
            // Arrange
            var validMgResources = new string[2] {
                "/providers/Microsoft.Management/managementGroups/wowMG//",
                "/providers/microsoft.management/ManagementGroups/CaseCorruptedMg" };

            var invalidMgResources = new string[2] {
                "/providers/Microsoft.Management/managementGroups/", // No MG Name
                "/providers/microsoft.management/ManagementGroups/MGPlusPlus/Microsoft.OhMyGod/HareKrishna" }; // MG Extension resource is not a MG resource

            // Act + Assert
            foreach (var testCase in validMgResources)
            {
                Assert.IsTrue(ResourceIdExtensions.IsValidManagementGroupDeprecated(testCase), "Should be valid MG!");
            }
            foreach (var testCase in invalidMgResources)
            {
                Assert.IsFalse(ResourceIdExtensions.IsValidManagementGroupDeprecated(testCase), "Should be Invalid MG!");
            }
        }

        [TestMethod]
        public void Test_IsValidManagementGroup()
        {
            // Arrange
            var validMgResources = new string[2] {
                "/providers/Microsoft.Management/managementGroups/wowMG",
                "/providers/microsoft.management/ManagementGroups/CaseCorruptedMg" };

            var invalidMgResources = new string[2] {
                "/providers/Microsoft.Management/managementGroups/", // No MG Name
                "/providers/microsoft.management/ManagementGroups/MGPlusPlus/Microsoft.OhMyGod/HareKrishna" }; // MG Extension resource is not a MG resource

            // Act + Assert
            foreach (var testCase in validMgResources)
            {
                Assert.IsTrue(ResourceIdExtensions.IsValidManagementGroup(testCase), "Should be valid MG!");
            }
            foreach (var testCase in invalidMgResources)
            {
                Assert.IsFalse(ResourceIdExtensions.IsValidManagementGroup(testCase), "Should be Invalid MG!");
            }
        }

        [TestMethod]
        public void Test_IsRootManagementGroup()
        {
            // Arrange
            var rootMgResource = "/providers/Microsoft.Management/managementGroups/9bd87791-c554-4e4b-8e21-4b70d8cb9c19";

            var nonRootMgResources = new string[2] {
                "/providers/Microsoft.Management/managementGroups/invalidGuidMG", // Invalid MG guid
                "/providers/microsoft.management/ManagementGroups/" }; // MG Extension resource is not a MG resource

            // Act + Assert
            Assert.IsTrue(ResourceIdExtensions.IsRootManagementGroup(rootMgResource, "9bd87791-c554-4e4b-8e21-4b70d8cb9c19"), "Should be valid Root MG!");

            foreach (var testCase in nonRootMgResources)
            {
                Assert.IsFalse(ResourceIdExtensions.IsRootManagementGroup(testCase, "invalidGuidMG"), "Should be Invalid Root MG!");
            }
        }

        #endregion

        #region Internal Util Tests

        [TestMethod]
        public void TestGetValue()
        {
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.GetValue("resourceGroups"));
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.FastGetValue("resourceGroups"));
        }

        [TestMethod]
        public void TestGetValueCaseInsensitive()
        {
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.GetValue(ResourceIdExtensions.ResourceGroupsSegment.ToUpperInvariant()));
            Assert.AreEqual("Default-Storage-EastUS", ResourcesConstants.SampleResourceId.FastGetValue(ResourceIdExtensions.ResourceGroupsSegment.ToUpperInvariant()));
        }

        [TestMethod]
        public void TestGetValueWithMalformattedKeys()
        {
            // Last char wrong - should return nothing
            var key = "resourceGroupt";
            Assert.IsNull(ResourcesConstants.SampleResourceId.GetValue(key));
            Assert.IsNull(ResourcesConstants.SampleResourceId.FastGetValue(key));

            // The key is only a prefix of a real segment key - should return nothing
            key = "resourceGroup";
            Assert.IsNull(ResourcesConstants.SampleResourceId.GetValue(key));
            Assert.IsNull(ResourcesConstants.SampleResourceId.FastGetValue(key));
        }

        #endregion

        #region ResourceId Util Tests

        [TestMethod]
        public void TestTryFixResourceIdFormat()
        {
            var id1 = "subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aai";
            var id2 = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS//providers/Microsoft.ClassicStorage/storageAccounts/aai";
            var id3 = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aai/";
            var validIdWithSpaceInName = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aa i";
            var validId = "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/aai";

            Assert.IsFalse(id1.IsValidResourceIdFormat());
            Assert.IsFalse(id2.IsValidResourceIdFormat());
            Assert.IsFalse(id3.IsValidResourceIdFormat());
            Assert.IsTrue(validIdWithSpaceInName.IsValidResourceIdFormat());
            Assert.IsTrue(id1.TryFixResourceIdFormat(out string fixId1, null));
            Assert.IsTrue(id2.TryFixResourceIdFormat(out string fixId2, null));
            Assert.IsTrue(id3.TryFixResourceIdFormat(out string fixId3, null));
            Assert.AreEqual(validId, fixId1);
            Assert.AreEqual(validId, fixId2);
            Assert.AreEqual(validId, fixId3);
        }

        #endregion

        [TestMethod]
        public void TestGetResourceLocation()
        {
            Assert.IsNull(ResourcesConstants.SampleResourceId.GetResourceLocation());
            Assert.IsNull(ResourcesConstants.ManagementGroupResourceId.GetResourceLocation());
            Assert.AreEqual("eastus", ResourcesConstants.QuotaAndUsageResourceId.GetResourceLocation());

            var resourceIdWithLocationAsResourceGroupName =
                "/subscriptions/6e9036d5-59fc-46d0-bb28-e3a5bccd90de/resourceGroups/Locations/providers/Microsoft.DocumentDb/databaseAccounts/mis-fixed-device";
            Assert.IsNull(resourceIdWithLocationAsResourceGroupName.GetResourceLocation());

            var resourceIdWithLocationInName =
                "/subscriptions/82f39621-cbc6-4e65-a9b9-4db2a3575c4f/resourceGroups/location/providers/Microsoft.Sql/servers/locations/databases/master	westus2";
            Assert.IsNull(resourceIdWithLocationInName.GetResourceLocation());

            var resourceIdWithLocationInName2 =
                "/subscriptions/0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef/resourceGroups/Default-Storage-EastUS/providers/Microsoft.ClassicStorage/storageAccounts/locations";
            Assert.IsNull(resourceIdWithLocationInName2.GetResourceLocation());
        }

        [TestMethod]
        public void TestQuotaAndUsageResource()
        {
            Assert.AreEqual("default", ResourcesConstants.QuotaAndUsageResourceId.GetResourceName());
            Assert.AreEqual("Microsoft.Compute/locations/usages", ResourcesConstants.QuotaAndUsageResourceId.GetResourceType());
            Assert.AreEqual("d1af5f8d-c2be-410e-8152-3b67724c58d7", ResourcesConstants.QuotaAndUsageResourceId.GetSubscriptionIdOrNull());
            Assert.IsNull(ResourcesConstants.QuotaAndUsageResourceId.GetResourceGroup());
        }

        [TestMethod]
        public void TestLocationResource()
        {
            Assert.AreEqual("westus", ResourcesConstants.LocationResourceId.GetResourceName());
            Assert.AreEqual("Microsoft.Resources/locations",
                ResourceIdExtensions.GetResourceTypeWithOverrides(null, ResourcesConstants.LocationResourceId));
            Assert.AreEqual("Microsoft.Resources/locations", ResourcesConstants.LocationResourceId.GetResourceType());
            Assert.AreEqual("0b88dfdb-55b3-4fb0-b474-5b6dcbe6b2ef", ResourcesConstants.LocationResourceId.GetSubscriptionIdOrNull());
            Assert.IsNull(ResourcesConstants.LocationResourceId.GetResourceGroup());
        }
    }
}