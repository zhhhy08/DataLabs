namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ResourceCacheUtils
    {
        private const int BufferPadding = 4;
        private const int TimeStampPadding = 8;
        private const byte MetaHeaderV1 = 0x57; // 01010111
        private const byte MetaHeaderV2 = 0x58; // 01011000

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetLowerCaseKeyWithArmId(string resourceId, string? tenantId)
        {
            var key = string.IsNullOrWhiteSpace(tenantId) ? resourceId : tenantId + resourceId;
            return key.ToLowerInvariant();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static string GetHashKeyString(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash1 = HashUtils.Murmur32(bytes);
            var hash2 = HashUtils.MurmurHash3x128(bytes);
            return hash2.Item1.ToString("x16") + hash2.Item2.ToString("x16") + hash1.ToString("x8");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetKeyBytes(ulong keyhash)
        {
            return BitConverter.GetBytes(keyhash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Memory<byte> CompressCacheValue(ResourceCacheDataFormat dataFormat, ReadOnlyMemory<byte> data, long timeStamp, string? etag)
        {
            using (var ms = new MemoryStream((data.Length / 2) + 32))
            {
                // Current UnixTime (Millisecond) uses around 41 bits.
                // Let's use first one byte to save Meta Bytes to indicate this is start of actual value
                // Unix Milli Timestmp will not use first byte until next  until 100000 year
                ms.WriteByte(MetaHeaderV2);

                // Add insertion TimeStamp
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ms.Write(BitConverter.GetBytes(currentTime), 0, TimeStampPadding);

                // Add Data Timestamp
                ms.Write(BitConverter.GetBytes(timeStamp), 0, TimeStampPadding);

                // Next byte indicates the data format enum
                ms.WriteByte(((byte)dataFormat));

                // Next byte indicates whether there is etag value or not
                if (string.IsNullOrEmpty(etag))
                {
                    ms.WriteByte(0);
                }
                else
                {
                    ms.WriteByte(1);
                    byte[] etagBytes = Encoding.ASCII.GetBytes(etag);
                    // Add Etag Length
                    ms.Write(BitConverter.GetBytes(etagBytes.Length), 0, BufferPadding);
                    // Include etag (etag is not compressed)
                    ms.Write(etagBytes);
                }

                // Include original bytes length
                ms.Write(BitConverter.GetBytes(data.Length), 0, BufferPadding);
                if (data.Length > 0)
                {
                    using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                    {
                        zip.Write(data.Span);
                    }
                }
                
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
            }
        }

        public static ResourceCacheResult DecompressCacheValue(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
            {
                return ResourceCacheResult.NoCacheEntry;
            }

            int startIndex = 0;
            string? etag = null;

            // Read first one byte for MetaHeader
            var metaByte = compressedBytes[startIndex];
            startIndex++;

            var versionByte = metaByte;
            if (versionByte != MetaHeaderV1 && versionByte != MetaHeaderV2)
            {
                // This might be because cache Entry was set with SetResourceIfGreaterThanAsync or SetValueIfMatchAsync
                // Read first 8 bytes which was saved with prefix by Garnet
                // Let's read from the begining again
                startIndex = 0;
                var cachePrefix = BitConverter.ToInt64(compressedBytes, startIndex);
                startIndex += TimeStampPadding;

                // Read next one byte again to check MetaHeader
                metaByte = compressedBytes[startIndex];
                startIndex++;

                versionByte = metaByte;
                if (versionByte != MetaHeaderV1 && versionByte != MetaHeaderV2)
                {
                    throw new InvalidDataException("No valid MetaHeader in Cache");
                }
            }

            long insertionTimestamp = 0;
            if (versionByte == MetaHeaderV2)
            {
                // V2 has insertion timestamp in the first 8 bytes
                // Read 8 bytes for timeStamp
                insertionTimestamp = BitConverter.ToInt64(compressedBytes, startIndex);
                startIndex += TimeStampPadding;
            }

            // Read first 8 bytes for timeStamp
            var dataTimestamp = BitConverter.ToInt64(compressedBytes, startIndex);
            startIndex += TimeStampPadding;

            // Read next byte for data Format
            ResourceCacheDataFormat dataFormat = (ResourceCacheDataFormat)compressedBytes[startIndex];
            startIndex++;

            // Read next byte for Etag
            byte hasEtag = compressedBytes[startIndex];
            startIndex++;

            if (hasEtag == 1)
            {
                // Read Etag Length
                var etagLength = BitConverter.ToInt32(compressedBytes, startIndex);
                startIndex += BufferPadding;

                if (etagLength > 0)
                {
                    // Read Etag
                    etag = Encoding.ASCII.GetString(compressedBytes, startIndex, etagLength);
                    startIndex += etagLength;
                }
            }
            
            var msgLength = BitConverter.ToInt32(compressedBytes, startIndex);
            startIndex += BufferPadding;

            if (msgLength > 0)
            {
                using var ms = new MemoryStream(compressedBytes, startIndex, compressedBytes.Length - startIndex);
                var buffer = new byte[msgLength];
                using (var zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    var offset = 0;
                    var remainingLength = buffer.Length;
                    while (true)
                    {
                        var bytesRead = zip.Read(buffer, offset, remainingLength);
                        offset += bytesRead;
                        remainingLength -= bytesRead;
                        if (bytesRead == 0 || remainingLength == 0)
                        {
                            break;
                        }
                    }
                }

                return new ResourceCacheResult(
                    found: true,
                    dataFormat: dataFormat,
                    array: buffer,
                    insertionTimestamp: insertionTimestamp,
                    dataTimestamp: dataTimestamp,
                    etag: etag);
            }
            else
            {
                return new ResourceCacheResult(
                    found: true,
                    dataFormat: dataFormat,
                    array: Array.Empty<byte>(),
                    insertionTimestamp: insertionTimestamp,
                    dataTimestamp: dataTimestamp,
                    etag: etag);
            }
        }
    }
}
