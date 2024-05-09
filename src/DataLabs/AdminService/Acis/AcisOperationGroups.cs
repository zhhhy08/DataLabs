namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis
{
    /// <summary>
    /// Geneva Action will be created for each controller.
    /// Actions are grouped in folders. Operation group defines second level folder.
    /// Important: Each controller must have exactly one operation group.
    /// Schema: Azure Resource Graph -> {Operation group} -> {Controller name}
    /// </summary>
    public static class AcisOperationGroups
    {
        #region DataLabs Groups

        public const string DataLabsOperationGroup = "Acis::OperationGroup::DataLabs";

        #endregion DataLabs Groups
    }
}