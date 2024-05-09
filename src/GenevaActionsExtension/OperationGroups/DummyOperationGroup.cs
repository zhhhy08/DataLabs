/*
 * NOTE: Currently, the Actions package builder requires at least a single
 * Operation defined in order to pass validation, therefore any 
 * API Gateway-enabled extensions must include at least one operation defined
 * via the SDK.
 *
 * This will be fixed in an upcoming version.
 *
 */
namespace Microsoft.WindowsAzure.Governance.DataLabs.GenevaActionsExtension.OperationGroups
{
    using Microsoft.WindowsAzure.Wapd.Acis.Contracts;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class DummyOperationGroup : AcisSMEOperationGroup
    {
        public override string Name => "Dummy Operation Group";
    }
}