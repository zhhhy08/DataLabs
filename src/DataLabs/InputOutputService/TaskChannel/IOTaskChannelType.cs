namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel
{
    using System;

    [Flags]
    public enum IOTaskChannelType
    {
        NONE = 0,
        RawInputChannel = 1 << 0,
        InputChannel = 1 << 1,
        InputCacheChannel = 1 << 2,
        PartnerChannel = 1 << 3,
        SourceOfTruthChannel = 1 << 4,
        OutputCacheChannel = 1 << 5,
        OutputChannel = 1 << 6,
        RetryChannel = 1 << 7,
        PoisonChannel = 1 << 8,
        FinalChannel = 1 << 9,
        SubJobChannel = 1 << 10,
        BlobPayloadRoutingChannel = 1 << 11,
    }
}
