namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;

    public static class CompressionHelper
    {
        private const int BufferPadding = 4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Compress(string text)
        {
            GuardHelper.ArgumentNotNullOrEmpty(text);

            var gzBufferAndLength = Compress(
                Encoding.UTF8.GetBytes(text));
            return Convert.ToBase64String(gzBufferAndLength.Buffer, 0, gzBufferAndLength.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (byte[] Buffer, int Length) Compress(byte[] bytes)
        {
            GuardHelper.ArgumentNotNull(bytes);

            using (var ms = new MemoryStream(bytes.Length / 2))
            {
                ms.Write(BitConverter.GetBytes(bytes.Length), 0, BufferPadding);
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(bytes, 0, bytes.Length);
                }
                return (ms.GetBuffer(), (int)ms.Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Memory<byte> Compress(ReadOnlyMemory<byte> data)
        {
            using (var ms = new MemoryStream(data.Length / 2))
            {
                ms.Write(BitConverter.GetBytes(data.Length), 0, BufferPadding);
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(data.Span);
                }
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<string> CompressAsync(string text)
        {
            GuardHelper.ArgumentNotNullOrEmpty(text);

            var gzBufferAndLength = await CompressAsync(
                Encoding.UTF8.GetBytes(text)).IgnoreContext();
            return Convert.ToBase64String(gzBufferAndLength.Buffer, 0, gzBufferAndLength.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<(byte[] Buffer, int Length)> CompressAsync(byte[] bytes)
        {
            GuardHelper.ArgumentNotNull(bytes);

            using (var ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(bytes.Length), 0, BufferPadding);
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    await zip.WriteAsync(bytes, 0, bytes.Length).IgnoreContext();
                }
                return (ms.GetBuffer(), (int)ms.Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Decompress(string compressedText)
        {
            GuardHelper.ArgumentNotNullOrEmpty(compressedText);

            var gzBuffer = Convert.
                FromBase64String(compressedText);
            return Encoding.UTF8.GetString(Decompress(gzBuffer));
        }

        public static byte[] Decompress(byte[] compressedBytes)
        {
            GuardHelper.ArgumentNotNull(compressedBytes);

            var msgLength = BitConverter.ToInt32(compressedBytes, 0);
            using (var ms = new MemoryStream(compressedBytes, BufferPadding, compressedBytes.Length - BufferPadding))
            {
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

                return buffer;
            }
        }

        public static byte[] Decompress(ReadOnlyMemory<byte> compressedBytes)
        {
            var msgLength = BitConverter.ToInt32(compressedBytes.Span);
            using (var ms = new ReadOnlyMemoryStream(compressedBytes.Slice(BufferPadding)))
            {
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
                return buffer;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<string> DecompressAsync(string compressedText)
        {
            GuardHelper.ArgumentNotNullOrEmpty(compressedText);

            var gzBuffer = Convert.
                FromBase64String(compressedText);
            return Encoding.UTF8.GetString(await DecompressAsync(gzBuffer).IgnoreContext());
        }

        public static async Task<byte[]> DecompressAsync(byte[] compressedBytes)
        {
            GuardHelper.ArgumentNotNull(compressedBytes);

            var msgLength = BitConverter.ToInt32(compressedBytes, 0);
            using (var ms = new MemoryStream(compressedBytes, BufferPadding, compressedBytes.Length - BufferPadding))
            {
                var buffer = new byte[msgLength];
                using (var zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    var offset = 0;
                    var remainingLength = buffer.Length;
                    while (true)
                    {
                        var bytesRead = await zip.ReadAsync(buffer, offset, remainingLength).IgnoreContext();
                        offset += bytesRead;
                        remainingLength -= bytesRead;
                        if (bytesRead == 0 || remainingLength == 0)
                        {
                            break;
                        }
                    }
                }

                return buffer;
            }
        }

        // A stream wrapper that counts the lenth of the bytes written.
        private class LengthCountingSerializationStream : Stream
        {
            public int SerializedLength
            {
                get;
                private set;
            }


            private Stream _baseStream;

            public LengthCountingSerializationStream(Stream baseStream)
            {
                GuardHelper.ArgumentNotNull(baseStream);

                this._baseStream = baseStream;
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush()
            {
                // Don't flush base stream as it increases the length of the output.
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this._baseStream.Write(buffer, offset, count);
                this.SerializedLength += count;
            }
        }
    }
}
