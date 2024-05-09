namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;

    public readonly struct ResourceCacheResult
    {
        public static readonly ResourceCacheResult NoCacheEntry = new(false, default, Array.Empty<byte>(), 0, 0, null);

        public readonly ResourceCacheDataFormat DataFormat;
        public readonly ReadOnlyMemory<byte> Content;
        public readonly long InsertionTimeStamp;
        public readonly long DataTimeStamp;
        public readonly string? Etag;
        public readonly bool Found;

        public ResourceCacheResult(bool found, ResourceCacheDataFormat dataFormat, byte[] array, long insertionTimestamp, long dataTimestamp, string? etag)
        {
            Found = found;
            DataFormat = dataFormat;
            Content = (array == null || array.Length == 0) ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(array);
            InsertionTimeStamp = insertionTimestamp;
            DataTimeStamp = dataTimestamp;
            Etag = etag;
        }
    }
}
