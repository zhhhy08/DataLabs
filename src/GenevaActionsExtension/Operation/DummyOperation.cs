/*
 * NOTE: Currently, the Actions package builder requires at least a single
 * Operation defined in order to pass validation, therefore any 
 * API Gateway-enabled extensions must include at least one operation defined
 * via the SDK.
 *
 * This will be fixed in an upcoming version.
 *
 */
namespace Microsoft.WindowsAzure.Governance.DataLabs.GenevaActionsExtension.Operation
{
    using Microsoft.WindowsAzure.Wapd.Acis.Contracts;
    using Microsoft.WindowsAzure.Wapd.Acis.Contracts.Models;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class DummyOperation : AcisSMESystemMetadataReadOnlyOperation
    {
        public override string OperationName => "Dummy";
        public override IEnumerable<IAcisSMEParameterRef> Parameters => new IAcisSMEParameterRef[0];
        public override IAcisSMEOperationGroup OperationGroup => new OperationGroups.DummyOperationGroup();
        public override IEnumerable<AcisUserClaim> ClaimsRequired => new[]
        {
            AcisSMESecurityGroup.PlatformServiceViewer
        };

        public override DataClassificationLevel DataClassificationLevel => DataClassificationLevel.NoCustomerContent;

        public IAcisSMEOperationResponse Dummy(IAcisServiceManagementExtension extension,
            IAcisSMEOperationProgressUpdater updater, IAcisSMEEndpoint endpoint)
        {
            return AcisSMEOperationResponseExtensions.StandardSuccessResponse("Success");
        }
    }
}
