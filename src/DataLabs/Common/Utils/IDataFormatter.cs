namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDataFormatter
    {
        T? ReadFromStream<T>(Stream readStream);

        Task<T?> ReadFromStreamAsync<T>(Stream readStream, CancellationToken cancellationToken);

        void WriteToStream<T>(T value, Stream writeStream);

        void WriteListItemToStream<T>(T value, Stream writeStream);

        Task WriteToStreamAsync<T>(T value, Stream writeStream, CancellationToken cancellationToken);
    }
}