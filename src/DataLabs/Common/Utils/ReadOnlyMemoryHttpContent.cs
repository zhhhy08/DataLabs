namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Net.Http;

    public sealed class ReadOnlyMemoryHttpContent : StreamContent
    {
        public ReadOnlyMemory<byte> MemoryContent;
        
        public ReadOnlyMemoryHttpContent(ReadOnlyMemory<byte> content) : base(new ReadOnlyMemoryStream(content))
        {
            MemoryContent = content;
        }
    }
}
