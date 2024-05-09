namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.ConsistentHash
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Runtime.CompilerServices;

    [TestClass]
    public class ConsistentHashTest
    {
        private List<HashSet<string>> _nodeKeySetList;
        private readonly int _numNodes = 7;
        private readonly int _numKeys = 1000 * 1000; // 1M keys
        private readonly int _numVnodes = 4096;
        private readonly string _keyPrefix = "/subscriptions/02d59989-f8a9-4b69-9919-1ef51df4eff6/resourceGroups/AzureResourcesCacheidm-Int-Solution-a/providers/Microsoft.Compute/virtualMachineScaleSets/idm";
        
        [TestInitialize]
        public void TestInitialize()
        {
            _nodeKeySetList = new List<HashSet<string>>(_numNodes);
            for (int i = 0; i < _numNodes; i++)
            {
                _nodeKeySetList.Add(new HashSet<string>());
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _nodeKeySetList?.Clear();
        }

        [TestMethod]
        public void HashModularTest()
        {
            AddCacheKeyToNode(_nodeKeySetList, GetHashModularNodeIndex);
            CheckResult(_nodeKeySetList, 1500);
            GetCacheKeyTest(_nodeKeySetList, GetHashModularNodeIndex);
        }

        [TestMethod]
        public void JumpConsistentHashTest()
        {
            AddCacheKeyToNode(_nodeKeySetList, HashUtils.JumpConsistentHash);
            CheckResult(_nodeKeySetList, 300);
            GetCacheKeyTest(_nodeKeySetList, HashUtils.JumpConsistentHash);
        }

        [TestMethod]
        public void RingConsistentHashTest()
        {
            TestConsistentHashingNode[] nodes = new TestConsistentHashingNode[_numNodes];

            for (int i = 0; i < _numNodes; i++)
            {
                nodes[i] = new TestConsistentHashingNode
                {
                    NodeId = i,
                    ConsistentHashingNodeName = $"Node{i}"
                };
            }
            var consistentHashing = new ConsistentHashing<TestConsistentHashingNode>(nodes, _numVnodes);
            AddCacheKeyToNode(_nodeKeySetList, (keyhash, numNodes) => consistentHashing.GetNode(keyhash).NodeId);
            CheckResult(_nodeKeySetList, 1500);
            GetCacheKeyTest(_nodeKeySetList, (keyhash, numNodes) => consistentHashing.GetNode(keyhash).NodeId);
        }

        [TestMethod]
        public void RingConsistentHashAddNodeTest()
        {
            // Calculate old Nodes
            RingConsistentHashTest();

            // Add a new node
            var newNumNodes = _numNodes + 1;

            var newNodeKeySetList = new List<HashSet<string>>(newNumNodes);
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodeKeySetList.Add(new HashSet<string>());
            }

            TestConsistentHashingNode[] newNodes = new TestConsistentHashingNode[newNumNodes];
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodes[i] = new TestConsistentHashingNode
                {
                    NodeId = i,
                    ConsistentHashingNodeName = $"Node{i}"
                };
            }

            var consistentHashing = new ConsistentHashing<TestConsistentHashingNode>(newNodes, _numVnodes);
            AddCacheKeyToNode(newNodeKeySetList, (keyhash, numNodes) => consistentHashing.GetNode(keyhash).NodeId);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("After Adding a new node");
            CheckResult(newNodeKeySetList, 1500);

            Console.WriteLine();
            Console.WriteLine();
            CheckKeysMoveToNewNodes(_nodeKeySetList, newNodeKeySetList, 18000, 1000);
        }

        [TestMethod]
        public void RingConsistentHashDeleteNodeTest()
        {
            // Calculate old Nodes
            RingConsistentHashTest();

            // Add a new node
            var newNumNodes = _numNodes - 1;

            var newNodeKeySetList = new List<HashSet<string>>(newNumNodes);
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodeKeySetList.Add(new HashSet<string>());
            }

            TestConsistentHashingNode[] newNodes = new TestConsistentHashingNode[newNumNodes];
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodes[i] = new TestConsistentHashingNode
                {
                    NodeId = i,
                    ConsistentHashingNodeName = $"Node{i}"
                };
            }

            var consistentHashing = new ConsistentHashing<TestConsistentHashingNode>(newNodes, _numVnodes);
            AddCacheKeyToNode(newNodeKeySetList, (keyhash, numNodes) => consistentHashing.GetNode(keyhash).NodeId);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("After Delete a new node");
            CheckResult(newNodeKeySetList, 1650);

            Console.WriteLine();
            Console.WriteLine();
            CheckKeysMoveToNewNodes(_nodeKeySetList, newNodeKeySetList, 20500, 50000);
        }


        [TestMethod]
        public void JumpConsistentHashAddNodeTest()
        {
            JumpConsistentHashTest();

            var newNumNodes = _numNodes + 1;
            var newNodeKeySetList = new List<HashSet<string>>(newNumNodes);
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodeKeySetList.Add(new HashSet<string>());
            }

            AddCacheKeyToNode(newNodeKeySetList, HashUtils.JumpConsistentHash);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("After Adding a new node");
            CheckResult(newNodeKeySetList, 300);

            Console.WriteLine();
            Console.WriteLine();
            CheckKeysMoveToNewNodes(_nodeKeySetList, newNodeKeySetList, 18000, 100);
        }

        [TestMethod]
        public void JumpConsistentHashDeleteNodeTest()
        {
            JumpConsistentHashTest();

            var newNumNodes = _numNodes - 1;
            var newNodeKeySetList = new List<HashSet<string>>(newNumNodes);
            for (int i = 0; i < newNumNodes; i++)
            {
                newNodeKeySetList.Add(new HashSet<string>());
            }

            AddCacheKeyToNode(newNodeKeySetList, HashUtils.JumpConsistentHash);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("After Delete a new node");
            CheckResult(newNodeKeySetList, 300);

            Console.WriteLine();
            Console.WriteLine();
            CheckKeysMoveToNewNodes(_nodeKeySetList, newNodeKeySetList, 20500, 50500);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetHashModularNodeIndex(ulong keyhash, int numNodes)
        {
            return (int)(keyhash % (ulong)numNodes);
        }

        private static void CheckKeysMoveToNewNodes(
            List<HashSet<string>> oldNodeKeySetList, 
            List<HashSet<string>> newNodeKeySetList, 
            double upperBoundMean,
            double upperBoundstdev)
        {
            int numOldNodes = oldNodeKeySetList.Count;
            int[] numMoved = new int[numOldNodes];
            for (int i = 0; i < numOldNodes; i++)
            {
                if (i >= newNodeKeySetList.Count)
                {
                    // Node is deleted
                    numMoved[i] = oldNodeKeySetList[i].Count;
                    continue;
                }

                var oldNodeKeySet = oldNodeKeySetList[i];
                var newNodeKeySet = newNodeKeySetList[i];

                foreach (var key in oldNodeKeySet)
                {
                    if (!newNodeKeySet.Contains(key))
                    {
                        // Key is moved
                        numMoved[i]++;
                    }
                }
            }

            for (int i = 0; i < numMoved.Length; i++)
            {
                var percentage = (double)numMoved[i] / oldNodeKeySetList[i].Count * 100;
                Console.WriteLine($"Node {i} moved {numMoved[i]} keys. Percentage: {percentage}%");
            }

            Console.WriteLine();
            Console.WriteLine();
            var mean = CalculateMean(numMoved);
            var stdev = CalculateStandardDeviation(numMoved);

            Console.WriteLine($"NumMoved Mean: {mean}");
            Console.WriteLine($"NumMoved Standard Deviation: {stdev}");

            Assert.IsTrue(mean < upperBoundMean);
            Assert.IsTrue(stdev < upperBoundstdev);
        }

        private void CheckResult(List<HashSet<string>> nodeKeySetList, double upperBoundstdev)
        {
            int totalKeys = 0;
            int numNodes = nodeKeySetList.Count;
            for (int i = 0; i < numNodes; i++)
            {
                var keyCount = nodeKeySetList[i].Count;
                totalKeys += keyCount;
                Console.WriteLine($"Node {i} has {keyCount} keys");
            }

            Assert.AreEqual(_numKeys, totalKeys);

            int[] keyCounts = new int[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                keyCounts[i] = nodeKeySetList[i].Count;
            }

            var mean = CalculateMean(keyCounts);
            var stdev = CalculateStandardDeviation(keyCounts);

            Console.WriteLine($"Mean: {mean}");
            Console.WriteLine($"Standard Deviation: {stdev}");

            Assert.IsTrue(stdev < upperBoundstdev);
        }

        private void AddCacheKeyToNode(List<HashSet<string>> nodeKeySetList, Func<ulong, int, int> nodeIdFunc)
        {
            for (int i = 0; i < _numKeys; i++)
            {
                var key = _keyPrefix + i;
                key = key.ToLowerInvariant();
                var cacheKey = ResourceCacheUtils.GetHashKeyString(key);
                var keyhash = CacheClientExecutor.GetKeyHash(cacheKey);

                int numNodes = nodeKeySetList.Count;
                var nodeId = nodeIdFunc(keyhash, numNodes);
                nodeKeySetList[nodeId].Add(cacheKey);
            }
        }

        private void GetCacheKeyTest(List<HashSet<string>> nodeKeySetList, Func<ulong, int, int> nodeIdFunc)
        {
            for (int i = 0; i < _numKeys; i++)
            {
                var key = _keyPrefix + i;
                key = key.ToLowerInvariant();
                var cacheKey = ResourceCacheUtils.GetHashKeyString(key);
                var keyhash = CacheClientExecutor.GetKeyHash(cacheKey);

                int numNodes = nodeKeySetList.Count;
                var nodeId = nodeIdFunc(keyhash, numNodes);
                Assert.IsTrue(nodeKeySetList[nodeId].Contains(cacheKey));
            }
        }

        private static double CalculateMean(int[] values)
        {
            double sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        private static double CalculateStandardDeviation(int[] values)
        {
            double mean = CalculateMean(values);
            double sum = 0;
            foreach (var value in values)
            {
                sum += Math.Pow(value - mean, 2);
            }
            return Math.Sqrt(sum / values.Length);
        }

        class TestConsistentHashingNode : IConsistentHashingNode
        {
            public string ConsistentHashingNodeName { get; set; }
            public int NodeId { get; set; }
        }
    }
}