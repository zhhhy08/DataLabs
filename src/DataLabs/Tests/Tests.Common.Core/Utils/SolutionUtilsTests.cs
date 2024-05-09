namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Utils
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System.Diagnostics;

    [TestClass]
    public class SolutionUtilsTests
    {
        [TestMethod]
        public void TestGetyTypeName()
        {
            Type type = typeof(AbstractTaskChannelManager<AbstractEventTaskContext<TestEventTaskContext>>);
            var typeName = SolutionUtils.GetTypeName(type);
            Assert.AreEqual("AbstractTaskChannelManager<AbstractEventTaskContext<TestEventTaskContext>>", typeName);
        }

        [TestMethod]
        public void TestGetyTypeName2()
        {
            Type type = typeof(SolutionUtils);
            var typeName = SolutionUtils.GetTypeName(type);
            Assert.AreEqual("SolutionUtils", typeName);
        }
    }
}
