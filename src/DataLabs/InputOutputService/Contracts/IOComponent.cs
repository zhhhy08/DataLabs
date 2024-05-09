namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    public enum IOComponent
    {
        RawInputChannel,
        InputChannel,
        PartnerChannel,
        PartnerLogic,
        PartnerRoutingManager,
        SourceOfTruthChannel,
        OutputChannel,
        RetryChannel,
        PoisonChannel,
        EventHubWriter,
        RetryQueueWriter,
        PoisonQueueWriter,
        StreamOutputEventTaskCallBack,
        ArnPublish,
        EventHubAsyncTaskInfoQueue,
        SubJobQueueWriter,
        BlobPayloadRoutingChannel
    }
}
