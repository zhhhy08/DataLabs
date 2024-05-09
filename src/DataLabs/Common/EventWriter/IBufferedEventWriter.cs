namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.EventWriter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;

    public interface IBufferedEventWriter<TOutput, TEvent> : IDisposable 
        where TOutput : class
        where TEvent : class
    {
        public IEventWriterCallBack<TOutput, TEvent>? EventWriterCallBack { get; set; }
        public ValueTask<bool> AddEventMessageAsync(IEventOutputContext<TOutput> eventOutputContext);
    }
}
