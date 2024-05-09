namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth
{
    using System;
    using System.Collections.Generic;

    public interface IPodHealthManager : IDisposable
    {
        public string ServiceName { get; }
        public HashSet<string> DenyListedNodes { get; }
        public void AddNodeToDenyList(IEnumerable<string> hosts);
        public void RemoveNodeFromDenyList(IEnumerable<string> hosts);
    }
}
