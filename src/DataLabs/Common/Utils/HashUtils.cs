namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;

    public static class HashUtils
    {
        #region Constants

        #region Murmur32 Magic Numbers

        private const uint Murmur32C1 = 0xcc9e2d51;
        private const uint Murmur32C2 = 0x1b873593;
        private const int Murmur32Rotate1 = 15;
        private const int Murmur32Rotate2 = 13;
        private const uint Murmur32M = 5;
        private const uint Murmur32N = 0xe6546b64;

        #endregion

        // Murmur32 reads 4 bytes at a time
        private const int Murmur32ReadSize = sizeof(uint);

        #endregion

        #region Public Methods

        public unsafe static uint Murmur32(byte[] bytes, uint seed = 0x1a2b3c4d)
        {
            var hash = seed;
            var remainingBytes = bytes.Length;
            var totalBytes = bytes.Length;
            var pos = 0;

            // NOTE: We are pinning the byte array for the entirety of hash calculation
            // This makes GC less efficient since the byte array cannot be relocated during GC compaction
            // However, since hash calculation is extremely cheap and fast, and that less frequent pinning yields better perf
            // We are accepting the tracde off here
            fixed (byte* bp = bytes)
            {
                while (remainingBytes >= Murmur32ReadSize)
                {
                    // Cast next 4 bytes as an uint
                    var val = *((uint*)(bp + pos));
                    pos += Murmur32ReadSize;
                    remainingBytes -= Murmur32ReadSize;

                    val = Murmur32Scramble(val);

                    hash ^= val;
                    hash = RotateLeft(hash, Murmur32Rotate2);
                    hash = (hash * Murmur32M) + Murmur32N;
                }

                if (remainingBytes > 0)
                {
                    hash ^= ProcessRemainingBytes(bp, remainingBytes, totalBytes);
                }
            }

            hash = FinalizeMurmur32(hash, totalBytes);

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMurmur32Hash(string textToHash, uint seed = 0)
        {
            GuardHelper.ArgumentNotNullOrEmpty(textToHash);

            var bytes = Encoding.UTF8.GetBytes(textToHash);
            return Murmur32(bytes, seed);
        }

        //
        // Summary:
        //     Murmurhash 3 - 64 bit
        //
        // Parameters:
        //   bString:
        //
        //   len:
        //
        //   seed:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ulong MurmurHash3x64(byte[] data, uint seed = 0u)
        {
            return MurmurHash3x128(data, seed).Item1;
        }

        //
        // Summary:
        //     Murmurhash 3 - 128 bit
        //
        // Parameters:
        //   bString:
        //
        //   len:
        //
        //   seed:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static (ulong, ulong) MurmurHash3x128(byte[] data, uint seed = 0x9f8e7d6c)
        {
            const ulong c1 = 0x87c37b91114253d5UL;
            const ulong c2 = 0x4cf5ad432745937fUL;
            const uint m = 5;
            const uint n = 0x100;

            ulong h1 = seed;
            ulong h2 = seed;

            fixed (byte* dataPtr = data)
            {
                int length = data.Length;
                byte* blockPtr = dataPtr;
                byte* endPtr = blockPtr + length / 16 * 16;

                while (blockPtr < endPtr)
                {
                    ulong k1 = *(ulong*)blockPtr;
                    ulong k2 = *(ulong*)(blockPtr + 8);
                    blockPtr += 16;

                    k1 *= c1;
                    k1 = (k1 << 31) | (k1 >> (64 - 31));
                    k1 *= c2;
                    h1 ^= k1;

                    h1 = (h1 << 27) | (h1 >> (64 - 27));
                    h1 += h2;
                    h1 = h1 * m + n;

                    k2 *= c2;
                    k2 = (k2 << 27) | (k2 >> (64 - 27));
                    k2 *= c1;
                    h2 ^= k2;

                    h2 = (h2 << 31) | (h2 >> (64 - 31));
                    h2 += h1;
                    h2 = h2 * m + n;
                }

                if (length % 16 != 0)
                {
                    ulong k1 = 0;
                    ulong k2 = 0;
                    byte* lastBlockPtr = endPtr;

                    switch (length & 15)
                    {
                        case 15:
                            k2 ^= (ulong)lastBlockPtr[14] << 48;
                            goto case 14;
                        case 14:
                            k2 ^= (ulong)lastBlockPtr[13] << 40;
                            goto case 13;
                        case 13:
                            k2 ^= (ulong)lastBlockPtr[12] << 32;
                            goto case 12;
                        case 12:
                            k2 ^= (ulong)lastBlockPtr[11] << 24;
                            goto case 11;
                        case 11:
                            k2 ^= (ulong)lastBlockPtr[10] << 16;
                            goto case 10;
                        case 10:
                            k2 ^= (ulong)lastBlockPtr[9] << 8;
                            goto case 9;
                        case 9:
                            k2 ^= (ulong)lastBlockPtr[8];
                            k2 *= c2;
                            k2 = (k2 << 27) | (k2 >> (64 - 27));
                            k2 *= c1;
                            h2 ^= k2;
                            goto case 8;
                        case 8:
                            k1 ^= *(ulong*)(lastBlockPtr);
                            k1 *= c1;
                            k1 = (k1 << 31) | (k1 >> (64 - 31));
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                        case 7:
                            k1 ^= (ulong)lastBlockPtr[6] << 48;
                            goto case 6;

                        case 6:
                            k1 ^= (ulong)lastBlockPtr[5] << 40;
                            goto case 5;
                        case 5:
                            k1 ^= (ulong)lastBlockPtr[4] << 32;
                            goto case 4;
                        case 4:
                            k1 ^= (ulong)lastBlockPtr[3] << 24;
                            goto case 3;
                        case 3:
                            k1 ^= (ulong)lastBlockPtr[2] << 16;
                            goto case 2;
                        case 2:
                            k1 ^= (ulong)lastBlockPtr[1] << 8;
                            goto case 1;
                        case 1:
                            k1 ^= (ulong)lastBlockPtr[0];
                            k1 *= c1;
                            k1 = (k1 << 31) | (k1 >> (64 - 31));
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                    }
                }
            }

            h1 ^= (ulong)data.Length;
            h2 ^= (ulong)data.Length;
            h1 += h2;
            h2 += h1;

            return (h1, h2);
        }

        public static ulong MD5_64BitHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = MD5.HashData(inputBytes);
            return BitConverter.ToUInt64(hashBytes, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // Return integer in the range 0..numBuckets-1.
        public static int JumpConsistentHash(ulong key, int num_buckets)
        {
            long b = -1;
            long j = 0;

            while (j < num_buckets)
            {
                b = j;
                key = key * 2862933555777941757UL + 1;
                j = (long)((b + 1) * ((double)(1L << 31) / (double)((key >> 33) + 1)));
            }
            return (int)b;
        }

        #endregion

        #region Private Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Murmur32Scramble(uint val)
        {
            val *= Murmur32C1;
            val = RotateLeft(val, Murmur32Rotate1);
            val *= Murmur32C2;

            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static uint ProcessRemainingBytes(byte* bp, int remaining, int total)
        {
            var val = 0U;

            // Loop unrolling
            switch (remaining)
            {
                case 3:
                    val = (uint)((*(bp + total - 1) << 16) | (*(bp + total - 2) << 8) | *(bp + total - 3));
                    break;
                case 2:
                    val = (uint)((*(bp + total - 2) << 8) | *(bp + total - 3));
                    goto case 1;
                case 1:
                    val = *(bp + total - 3);
                    break;
                default:
                    break;
            }

            return Murmur32Scramble(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint original, int count)
        {
            if (count >= 32)
            {
                count %= 32;
            }
            return (original << count) | (original >> (32 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FinalizeMurmur32(uint hash, int totalBytes)
        {
            hash ^= (uint)totalBytes; // This case is always safe, since we are casting from int to uint
            hash ^= (hash >> 16);
            hash *= 0x85ebca6b;
            hash ^= (hash >> 13);
            hash *= 0xc2b2ae35;
            hash ^= (hash >> 16);

            return hash;
        }

        #endregion
    }
}