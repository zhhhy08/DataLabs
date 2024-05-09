namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /*
     * For simplicity and to avoid unnecessary locking, this class doesn't have add/remove node methods.
     * When we add a node or remove Node, we need to create a new instance of ConsistentHashing class
     */
    public class ConsistentHashing<T> where T : IConsistentHashingNode
    {
        public int NumNodes { get; }
        public int VirtualNodeCount { get; }

        private readonly ulong[] _sortedHashValues;
        private readonly T[] _nodes;

        public ConsistentHashing(T[] nodes, int virtualNodeCount)
        {
            GuardHelper.ArgumentNotNullOrEmpty(nodes);
            GuardHelper.IsArgumentPositive(virtualNodeCount);

            VirtualNodeCount = virtualNodeCount;
            NumNodes = nodes.Length;

            int totalVNodes = nodes.Length * virtualNodeCount;
            var vnodeTuples = new List<(ulong, T)>(totalVNodes);

            for (int i = 0; i < nodes.Length; i++)
            {
                T node = nodes[i];
                for (int j = 0; j < virtualNodeCount; j++)
                {
                    var virtualNode = $"{node.ConsistentHashingNodeName}_VirtualNode_{j}";
                    var hash = CalculateHash(virtualNode);
                    vnodeTuples.Add((hash, node));
                }
            }

            // Sort the tuples by hash
            vnodeTuples.Sort((x, y) => x.Item1.CompareTo(y.Item1));

            _sortedHashValues = new ulong[vnodeTuples.Count];
            _nodes = new T[vnodeTuples.Count];

            for (int i = 0; i < vnodeTuples.Count; i++)
            {
                _sortedHashValues[i] = vnodeTuples[i].Item1;
                _nodes[i] = vnodeTuples[i].Item2;
            }
        }

        public T GetNode(ulong hash)
        {
            int index = Array.BinarySearch(_sortedHashValues, hash);

            if (index < 0)
            {
                // If the index is negative, it represents the bitwise
                // complement of the next larger element in the array.
                //
                index = ~index;
                if (index == _sortedHashValues.Length)
                {
                    index = 0;
                }
            }

            return _nodes[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong CalculateHash(string input)
        {
            return HashUtils.MD5_64BitHash(input);
        }
    }

    public interface IConsistentHashingNode
    {
        public string ConsistentHashingNodeName { get; }
    }
}
