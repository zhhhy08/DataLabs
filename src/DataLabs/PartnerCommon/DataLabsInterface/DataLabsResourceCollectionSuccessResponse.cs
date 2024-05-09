namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;

    public class DataLabsResourceCollectionSuccessResponse
    {
        public readonly List<GenericResource>? Value;

        public DataLabsResourceCollectionSuccessResponse(List<GenericResource>? value)
        {
            Value = value;
        }
    }
}
