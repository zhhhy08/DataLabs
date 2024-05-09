namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [Flags]
    public enum IOEventTaskFlag : long
    {
        NONE = 0L,
        DependentResource = 1L << 0,
        NeedToAddInputCache = 1L << 1,
        AddedToRawInputChannel = 1L << 2,
        AddedToInputChannel = 1L << 3,
        AddedToInputCacheChannel = 1L << 4,
        AddedToPartnerChannel = 1L << 5,
        AddedToOutputChannel = 1L << 6,
        AddedToSourceOfTruthChannel = 1L << 7,
        AddedToRetryChannel = 1L << 8,
        AddedToPoisonChannel = 1L << 9,
        AddedToFinalChannel = 1L << 10,
        SuccessUploadToSourceOfTruth = 1L << 11,
        FailedToMoveToRetry = 1L << 12,
        FailedToMoveToPoison = 1L << 13,
        EventHubBatchWriteSuccess = 1L << 14,
        EventHubBatchWriteFail = 1L << 15,
        RetryQueueBatchWriteSuccess = 1L << 16,
        RetryQueueBatchWriteFail = 1L << 17,
        PoisonQueueBatchWriteSuccess = 1L << 18,
        PoisonQueueBatchWriteFail = 1L << 19,
        SourceOfTruthEtagConflict = 1L << 20,
        SourceOfTruthOutputTimeConflict = 1L << 21,
        DeleteCacheAfterSourceOfTruthETagConflict = 1L << 22,
        RetrySourceOfTruthConflict = 1L << 23,
        PartnerRetryResponse = 1L << 24,
        PartnerPoisonResponse = 1L << 25,
        PartnerDropResponse = 1L << 26,
        PartnerEmptyResponse = 1L << 27,
        PartnerSuccessResponse = 1L << 28,
        PartnerSubJobResponse = 1L << 29,
        ArnPublishSuccess = 1L << 30,
        ArnPublishFail = 1L << 31,
        AddedToSubJobChannel = 1L << 32,
        SubJobQueueBatchWriteSuccess = 1L << 33,
        SubJobQueueBatchWriteFail = 1L << 34,
        AddedToBlobPayloadRoutingChannel = 1L << 35,
        BlobPayloadRoutingSuccess = 1L << 36,
        BlobPayloadRoutingFail = 1L << 37,
        RetryTaskChannelOverWriteBlobPayloadRouting = 1L << 38,
        SuccessInputCacheWrite = 1L << 39,
        RetrySuccessInputCacheWrite = 1L << 40
    }

    public static class IOEventTaskFlagHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSuccessFinalStage(EventTaskFinalStage eventTaskFinalStage)
        {
            return eventTaskFinalStage == EventTaskFinalStage.SUCCESS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetPartnerResponseFlags(this IOEventTaskFlag flag)
        {
            if ((flag & IOEventTaskFlag.PartnerSuccessResponse) != 0L)
            {
                return IOEventTaskFlag.PartnerSuccessResponse.FastEnumToString();
            }

            if ((flag & IOEventTaskFlag.PartnerEmptyResponse) != 0L)
            {
                return IOEventTaskFlag.PartnerEmptyResponse.FastEnumToString();
            }

            if ((flag & IOEventTaskFlag.PartnerRetryResponse) != 0L)
            {
                return IOEventTaskFlag.PartnerRetryResponse.FastEnumToString();
            }

            if ((flag & IOEventTaskFlag.PartnerPoisonResponse) != 0L)
            {
                return IOEventTaskFlag.PartnerPoisonResponse.FastEnumToString();
            }

            if ((flag & IOEventTaskFlag.PartnerDropResponse) != 0L)
            {
                return IOEventTaskFlag.PartnerDropResponse.FastEnumToString();
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTaskInFinalStageChannel(IOEventTaskFlag flag)
        {
            return (flag & IOEventTaskFlag.AddedToRetryChannel) != 0L ||
                (flag & IOEventTaskFlag.AddedToPoisonChannel) != 0L ||
                (flag & IOEventTaskFlag.AddedToFinalChannel) != 0L;
        }

        // IOEventTaskFlag Helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDependentResource(IOEventTaskFlag flag)
        {
            return (flag & IOEventTaskFlag.DependentResource) != 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NeedAddToInputCache(IOEventTaskFlag flag)
        {
            if ((flag & IOEventTaskFlag.RetrySuccessInputCacheWrite) != 0L)
            {
                // Cache has been already successfully added to InputCache
                // We don't need to add it again
                return false;
            }

            return IsDependentResource(flag) || ((flag & IOEventTaskFlag.NeedToAddInputCache) != 0L);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasSourceOfTruthConflict(IOEventTaskFlag flag)
        {
            return ((flag & IOEventTaskFlag.SourceOfTruthEtagConflict) != 0L) ||
                ((flag & IOEventTaskFlag.SourceOfTruthOutputTimeConflict) != 0L);
        }
    }
}