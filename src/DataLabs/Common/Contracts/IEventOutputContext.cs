namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    public interface IEventOutputContext<TOutput>
    {
        public CancellationToken TaskCancellationToken { get; }

        public ActivityContext ParentActivityContext { get; }

        public BinaryData GetOutputMessage();

        public int GetOutputMessageSize();

        public TOutput GetWrappedContext();
    }
}
