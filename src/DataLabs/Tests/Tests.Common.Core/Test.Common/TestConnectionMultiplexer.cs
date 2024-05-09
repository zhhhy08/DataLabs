namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using StackExchange.Redis;
    using StackExchange.Redis.Maintenance;
    using StackExchange.Redis.Profiling;

    [ExcludeFromCodeCoverage]
    public class TestConnectionMultiplexer : IConnectionMultiplexer
    {
        public TestCacheNode TestCacheNode { get; }
        public TestDatabase TestDatabase { get; }

        public TestConnectionMultiplexer(TestCacheNode testCacheNode)
        {
            TestCacheNode = testCacheNode;
            TestDatabase = new TestDatabase(this);
        }

        public event EventHandler<RedisErrorEventArgs> ErrorMessage { add {} remove {} }
        public event EventHandler<ConnectionFailedEventArgs> ConnectionFailed { add { } remove { } }
        public event EventHandler<InternalErrorEventArgs> InternalError { add { } remove { } }
        public event EventHandler<ConnectionFailedEventArgs> ConnectionRestored { add { } remove { } }
        public event EventHandler<EndPointEventArgs> ConfigurationChanged { add { } remove { } }
        public event EventHandler<EndPointEventArgs> ConfigurationChangedBroadcast { add { } remove { } }
        public event EventHandler<ServerMaintenanceEvent> ServerMaintenanceEvent { add { } remove { } }
        public event EventHandler<HashSlotMovedEventArgs> HashSlotMoved { add { } remove { } }

        public IDatabase GetDatabase(int db = -1, object asyncState = null)
        {
            return TestDatabase;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public string ClientName => throw new NotImplementedException();

        public string Configuration => throw new NotImplementedException();

        public int TimeoutMilliseconds => throw new NotImplementedException();

        public long OperationCount => throw new NotImplementedException();

        public bool PreserveAsyncOrder { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsConnected => throw new NotImplementedException();

        public bool IsConnecting => throw new NotImplementedException();

        public bool IncludeDetailInExceptions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int StormLogThreshold { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Close(bool allowCommandsToComplete = true)
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync(bool allowCommandsToComplete = true)
        {
            throw new NotImplementedException();
        }

        public bool Configure(TextWriter log = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ConfigureAsync(TextWriter log = null)
        {
            throw new NotImplementedException();
        }

        public void ExportConfiguration(Stream destination, ExportOptions options = (ExportOptions)(-1))
        {
            throw new NotImplementedException();
        }

        public ServerCounters GetCounters()
        {
            throw new NotImplementedException();
        }

        public EndPoint[] GetEndPoints(bool configuredOnly = false)
        {
            throw new NotImplementedException();
        }

        public int GetHashSlot(RedisKey key)
        {
            throw new NotImplementedException();
        }

        public IServer GetServer(string host, int port, object asyncState = null)
        {
            throw new NotImplementedException();
        }

        public IServer GetServer(string hostAndPort, object asyncState = null)
        {
            throw new NotImplementedException();
        }

        public IServer GetServer(IPAddress host, int port)
        {
            throw new NotImplementedException();
        }

        public IServer GetServer(EndPoint endpoint, object asyncState = null)
        {
            throw new NotImplementedException();
        }

        public IServer[] GetServers()
        {
            throw new NotImplementedException();
        }

        public string GetStatus()
        {
            throw new NotImplementedException();
        }

        public void GetStatus(TextWriter log)
        {
            throw new NotImplementedException();
        }

        public string GetStormLog()
        {
            throw new NotImplementedException();
        }

        public ISubscriber GetSubscriber(object asyncState = null)
        {
            throw new NotImplementedException();
        }

        public int HashSlot(RedisKey key)
        {
            throw new NotImplementedException();
        }

        public long PublishReconfigure(CommandFlags flags = CommandFlags.None)
        {
            throw new NotImplementedException();
        }

        public Task<long> PublishReconfigureAsync(CommandFlags flags = CommandFlags.None)
        {
            throw new NotImplementedException();
        }

        public void RegisterProfiler(Func<ProfilingSession> profilingSessionProvider)
        {
            throw new NotImplementedException();
        }

        public void ResetStormLog()
        {
            throw new NotImplementedException();
        }

        public void Wait(Task task)
        {
            throw new NotImplementedException();
        }

        public T Wait<T>(Task<T> task)
        {
            throw new NotImplementedException();
        }

        public void WaitAll(params Task[] tasks)
        {
            throw new NotImplementedException();
        }

        public void AddLibraryNameSuffix(string suffix)
        {
            throw new NotImplementedException();
        }
    }
}