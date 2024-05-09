namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System;
    using Polly;

    internal class TestEventOutputContext : IEventOutputContext<TestEventOutputContext>
    {
        private readonly ActivityContext _topLevelActivity;
        internal BinaryData OutputData { get; set; }

        public TestEventOutputContext()
        {
            _topLevelActivity = Tracer.CreateNewActivityContext();
        }

        public CancellationToken TaskCancellationToken => CancellationToken.None;

        public ActivityContext ParentActivityContext => _topLevelActivity;

        public BinaryData GetOutputMessage() => OutputData;

        public int GetOutputMessageSize() => (int)OutputData.ToStream().Length;

        public TestEventOutputContext GetWrappedContext() => this;
    }
}
