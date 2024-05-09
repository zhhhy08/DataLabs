using System.Diagnostics.CodeAnalysis;
using System.Text;

[ExcludeFromCodeCoverage]
class Program
{
    static async Task Main(string[] args)
    {
        await WriteToFileAsync();
    }

    public static async Task WriteToFileAsync()
    {
        var httpclient = new HttpClient(new HttpClientHandler() { CheckCertificateRevocationList = true });
        try
        {
            var serviceName = "DataLabs";
            var directoryToWrite = Directory.GetParent(Directory.GetCurrentDirectory());
            while (directoryToWrite != null && !directoryToWrite.Name.EndsWith("DataLabs", StringComparison.OrdinalIgnoreCase))
            {
                directoryToWrite = directoryToWrite.Parent;
            }
            if (directoryToWrite == null)
            {
                throw new InvalidOperationException("Unable to find the folder, please verify where project is being run from.");
            }
            var pathToWrite = directoryToWrite.FullName +
            $"/src/GenevaActionsExtension/swagger_{serviceName}.json";

            using (var stream = new StreamWriter(pathToWrite, false, Encoding.UTF8))
            {
                var serviceNameStr = serviceName.ToString().ToLower();
                var content = await httpclient.GetStringAsync($"http://localhost:5000/swagger/{serviceNameStr}/swagger.json");
                content = content.Replace("\n", "\r\n");
                stream.Write(content);
                stream.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            // not throw for now
        }
        finally
        {
            httpclient.Dispose();
        }
    }
}