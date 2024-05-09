namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CAS
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System.Collections.Generic;

    public class ModernCustomerSegmentKeyEqualityComparer : IEqualityComparer<ModernCustomerSegmentKey>
    {
        public bool Equals(ModernCustomerSegmentKey? key1, ModernCustomerSegmentKey? key2)
        {
            GuardHelper.ArgumentNotNull(key1, nameof(key1));
            GuardHelper.ArgumentNotNull(key2, nameof(key2));

            return key1.Equals(key2);
        }

        public int GetHashCode(ModernCustomerSegmentKey key)
        {
            return key.GetHashCode();
        }
    }
}
