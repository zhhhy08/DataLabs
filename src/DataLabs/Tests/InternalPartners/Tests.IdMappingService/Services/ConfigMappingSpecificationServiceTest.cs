namespace Microsoft.WindowsAzure.IdMappingService.Tests.Services
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.IdMappingService.Services;
    using System;

    //TODO this class should use mocking and not rely on the actual config file
    [TestClass]
    public class ConfigMappingSpecificationServiceTest
    {
        [TestInitialize]
        public void Initialize()
        {

        }

        [TestMethod]
        public void TestGetInternalIdSpecification()
        {
            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var cloudServicesSpec = configMappingSpecificationService.GetInternalIdSpecification("Microsoft.Compute/CloudServices");

            Assert.AreEqual("UniqueId", cloudServicesSpec.InternalIdPaths[0].Name);
            Assert.AreEqual("properties.uniqueId", cloudServicesSpec.InternalIdPaths[0].Path);
        }

        [TestMethod]
        public void TestGetInternalIdSpecificationInvalidResourceType()
        {
            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            Assert.ThrowsException<ArgumentException>(() => configMappingSpecificationService.GetInternalIdSpecification("Microsoft.Compute/FakeResourceType"));
        }

        [TestMethod]
        public void TestGetInternalIdSpecificationCaseInsensitivity()
        {
            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var cloudServicesSpec = configMappingSpecificationService.GetInternalIdSpecification("MICROsoft.COMPUTE/CloudServices");

            Assert.AreEqual("UniqueId", cloudServicesSpec.InternalIdPaths[0].Name);
            Assert.AreEqual("properties.uniqueId", cloudServicesSpec.InternalIdPaths[0].Path);
        }

        [TestMethod]
        public void TestGetInternalIdWithMultipleResults()
        {
            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var virtualMachineSpec = configMappingSpecificationService.GetInternalIdSpecification("Microsoft.Compute/VirtualMachines");

            var sortedVirtualMachineInternalIds = virtualMachineSpec.InternalIdPaths.OrderBy(x => x.Name).ToList();
            Assert.AreEqual("VmId", sortedVirtualMachineInternalIds[0].Name);
            Assert.AreEqual("properties.vmId", sortedVirtualMachineInternalIds[0].Path);

            Assert.AreEqual("VmssId", sortedVirtualMachineInternalIds[1].Name);
            Assert.AreEqual("properties.vmId", sortedVirtualMachineInternalIds[1].Path);
            Assert.AreEqual("properties.virtualMachineScaleSet.id", sortedVirtualMachineInternalIds[1].ArmIdPath);
            Assert.IsTrue(sortedVirtualMachineInternalIds[1].IsOptional);
        }

        [TestMethod]
        public void TestGetInternalIdWithOverrideResourceType()
        {
            var configMappingSpecificationService = new ConfigMappingSpecificationService();
            var virtualMachineSpec = configMappingSpecificationService.GetInternalIdSpecification("Microsoft.Compute/VirtualMachineScaleSets/VirtualMachines");
            var sortedVirtualMachineInternalIds = virtualMachineSpec.InternalIdPaths.OrderBy(x => x.Name).ToList();

            Assert.AreEqual("VmId", sortedVirtualMachineInternalIds[0].Name);
            Assert.AreEqual("properties.vmId", sortedVirtualMachineInternalIds[0].Path);

            Assert.AreEqual("VmssIdOrVmId", sortedVirtualMachineInternalIds[1].Name);
            Assert.AreEqual("properties.vmId", sortedVirtualMachineInternalIds[1].Path);
            Assert.AreEqual("Microsoft.Compute/VirtualMachines", sortedVirtualMachineInternalIds[1].OverrideResourceTypeIndex);
        }
    }
}
