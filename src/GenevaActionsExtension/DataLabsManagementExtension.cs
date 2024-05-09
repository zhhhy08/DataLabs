namespace Microsoft.WindowsAzure.Governance.DataLabs.GenevaActionsExtension
{
    using Microsoft.WindowsAzure.Wapd.Acis.Contracts;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Creates instances of <see cref="DataLabsManagementExtension" />.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DataLabsManagementExtension : AcisServiceManagementExtension
    {
        private static readonly string ServiceNameConst = "AzureResourceBuilder-DataLabs";

        public override string ExtensionVersion => "1.0";

        /// <summary>
        /// Gets extension name
        /// Uniquely identifies the Extension.
        /// </summary>
        public override string ServiceName { get => ServiceNameConst; }


        /// <summary>
        /// Always called when extension is loaded, allows dynamic creation of
        ///  endpoints and general initialization.
        /// </summary>
        /// <returns></returns>
        public override bool OnLoad()
        {
            Logger.LogVerbose(string.Format("Just loaded {0}", ServiceName));
            return true;
        }

        /// <summary>
        /// Each endpoint that is created is initialized and then this method is called
        /// providing that endpoint.  Unlike our other extension points, you can't extend
        /// an endpoint - it is populated only from your configuration file.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public override bool OnEndpointCreate(IAcisSMEEndpoint endpoint)
        {
            // Report on the core information for the extension
            Logger.LogVerbose(string.Format("Extension {0} creating endpoint {1}", endpoint.ContainingExtension.ServiceName, endpoint.Name));
            Logger.LogVerbose(string.Format(".. operations provided are {0}", string.Join("|", endpoint.Operations.Select(op => op.ToString()))));

            // Report on the configuration contained in the endpoint - the Geneva Actions infrastructure doesn't rely on any of this
            //  configuration it's purely for the extension's use
            Logger.LogVerbose(string.Format(".. configuration defines environment as {0}", endpoint.Configuration.GetConfigurationValue("env")));
            return true;
        }
    }
}