namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class StreamUtils
    {
        public static MemoryStream CreateMemoryStream(ReadOnlyMemory<byte> memory, bool isReadOnly)
        {
            if (memory.IsEmpty)
            {
                // Return an empty stream if the memory was empty
                return new MemoryStream(0);
            }

            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
            {
                return new MemoryStream(segment.Array!, segment.Offset, segment.Count, !isReadOnly);
            }

            return new MemoryStream(memory.ToArray(), !isReadOnly);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemoryStream CreateReadOnlyMemoryStream(ReadOnlyMemory<byte> content)
        {
            return new ReadOnlyMemoryStream(content);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemoryStream CreateReadOnlyMemoryStream(byte[] content)
        {
            return new ReadOnlyMemoryStream(content);
        }
    }
}
