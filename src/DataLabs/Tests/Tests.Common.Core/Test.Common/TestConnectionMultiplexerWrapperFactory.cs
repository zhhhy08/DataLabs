namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using StackExchange.Redis;

    [ExcludeFromCodeCoverage]
    public class TestConnectionMultiplexerWrapperFactory : IConnectionMultiplexerWrapperFactory
    {
        public Dictionary<string, TestCacheNode> TestCacheClientMap = new();

        public IConnectionMultiplexerWrapper CreateConnectionMultiplexerWrapper(DataLabCacheNode dataLabCacheNode)
        {
            var nodeName = dataLabCacheNode.CacheNodeName;
            if (!TestCacheClientMap.TryGetValue(nodeName, out var testCacheNode))
            {
                testCacheNode = new TestCacheNode();
                TestCacheClientMap.Add(nodeName, testCacheNode);
            }

            return new TestConnectionMultiplexerWrapper(dataLabCacheNode, testCacheNode);
        }

        public class TestConnectionMultiplexerWrapper : IConnectionMultiplexerWrapper
        {
            public TestCacheNode TestCacheNode { get; }
            public DataLabCacheNode DataLabCacheNode { get; }
            public TestConnectionMultiplexer TestConnectionMultiplexer { get; }

            public TestConnectionMultiplexerWrapper(DataLabCacheNode dataLabCacheNode, TestCacheNode testCacheNode)
            {
                DataLabCacheNode = dataLabCacheNode;
                TestCacheNode = testCacheNode;
                TestConnectionMultiplexer = new TestConnectionMultiplexer(testCacheNode);
            }

            public ValueTask<IConnectionMultiplexer> CreateConnectionMultiplexerAsync(IActivity activity, CancellationToken cancellationToken)
            {
                return ValueTask.FromResult((IConnectionMultiplexer)TestConnectionMultiplexer);
            }

            public void Dispose()
            {
            }
        }
    }
}