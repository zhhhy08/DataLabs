namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts
{
    public enum RetryReason
    {
        None = 0,
        PayloadDisassemblyError,
        PartnerLogic,
        SourceOfTruthEtagConflict,
        StreamChildTaskError,
        EventHubMaxDurationExpire,
        LargeBatchedRawInput,
        TaskCanceled
    }

    public enum PoisonReason
    {
        None = 0,
        DeserializeError,
        PartnerBlobNonRetryableCode,
        TaskCancelled,
        PartnerLogic,
        LargeSizeOutput,
        RetryQueueWriteFail,
        MaxRetryLimit,
        RetryStrategy,
        StreamChildTaskError,
        NotAllowedOutputResourceType,
        NoResourceInNotification,
        EtagConflictAndManyResponses,
        StreamChildErrorAndManyResponses
    }

    public enum DropReason
    {
        None = 0,
        PartnerLogic,
        LargeSizeOutput,
        OlderOutputMessage,
        PoisonInsidePoison,
        TaskCancelCalled,
        TaskFiltered
    }
}
