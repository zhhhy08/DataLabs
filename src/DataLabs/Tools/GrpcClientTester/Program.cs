using Grpc.Health.V1;
using Grpc.Net.Client;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

[ExcludeFromCodeCoverage]
internal class Program
{
    private static async Task Main(string[] args)
    {
        // IO Service
        var channel = GrpcChannel.ForAddress("http://localhost:5071");

        // Partner Service
        //var channel = GrpcChannel.ForAddress("http://localhost:5072");

        // ResourceFetcherProxy Service
        //var channel = GrpcChannel.ForAddress("http://localhost:5073");


        var client = new Health.HealthClient(channel);
        var healthCheckRequest = new HealthCheckRequest();

        System.Console.WriteLine();
        System.Console.WriteLine("Service: " + healthCheckRequest.Service);

        var response = await client.CheckAsync(healthCheckRequest).ConfigureAwait(false);
        var status = response.Status;

        System.Console.WriteLine();
        System.Console.WriteLine("Status: " + status.ToString());
    }
}