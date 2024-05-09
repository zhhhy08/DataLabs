namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface
{
    using System.Collections.Generic;

    public class IdMappingRequestBody
    {
        public required IEnumerable<string> AliasResourceIds { get; set; }
    }
}
