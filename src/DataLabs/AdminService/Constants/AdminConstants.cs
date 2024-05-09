namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants
{
    public class AdminConstants
    {
        // Endpoints
        public const string AdminEndpointSuffix = "AdminEndpoint";
        public const string ResourceProxyAdminEndpoint = "ResourceProxyAdminEndpoint";
        public const string IOServiceAdminEndpoint = "IOServiceAdminEndpoint";
        public const string ResourceFetcherAdminEndpoint = "ResourceFetcherAdminEndpoint";

        // ResourceProxy Actions
        public const string BaseResourceProxyRoute = "admin/resourceproxy";
        public const string GetConfiguration = "getconfiguration";
        public const string ConfigKey = "configkey";
        public const string ConfigValue = "configvalue";

        // IOService Actions
        public const string BaseIOServiceRoute = "admin/ioservice";
        public const string EventHubRoute = "eventhub";
        public const string UpdateCheckpointAndRestart = "updatecheckpointandrestart";
        public const string Seconds = "seconds";

        // Service Bus Actions
        public const string DeleteAndRecreateServiceBusQueue = "deleteandrecreateservicebusqueue";
        public const string DeleteDeadLetterMessages = "deletedeadlettermessages";
        public const string ReplayDeadLetterMessages = "replaydeadlettermessages";
        public const string QueueName = "queuename";
        public const string NeedDelete = "needdelete";
        public const string ReplayLookBackHours = "replaylookbackhours";
        public const string DeleteLookBackHours = "deletelookbackhours";
        public const string NumberOfNodesInParallel = "numberofnodesinparallel";
        public const string UtcNowFileTime = "utcnowfiletime";

        // Kubernetes Actions
        public const string GetPods = "GetPods";
        public const string GetAllPods = "GetAllPods";
        public const string GetAllConfigMaps = "GetAllConfigMaps";
        public const string GetPodLogs = "GetPodLogs";
        public const string GetDeployment = "GetDeployment";
        public const string GetDaemonSet = "GetDaemonSet";
        public const string UpdateConfigMapKey = "UpdateConfigMapKey";
        public const string UpdateMultipleConfigMapKeys = "UpdateMultipleConfigMapKeys";
        public const string DeletePod = "DeletePod";
        public const string ScaleDeploymentReplicaCount = "ScaleDeploymentReplicaCount";
        public const string RestartDeployment = "RestartDeployment";
        public const string RestartDaemonSet = "RestartDaemonSet";

        public const string ContainerName = "ContainerName";
        public const string PodName = "PodName";
        public const string PodNamespace = "PodNamespace";
        public const string ConfigMapName = "ConfigMapName";
        public const string ConfigMapNamespace = "ConfigMapNamespace";
        public const string ConfigMapKey = "ConfigMapKey";
        public const string ConfigMapValue = "ConfigMapValue";
        public const string ConfigMapOverride = "ConfigMapOverride";
        public const string DaemonSetName = "DaemonSetName";
        public const string DaemonSetNamespace = "DaemonSetNamespace";
        public const string DeploymentName = "DeploymentName";
        public const string DeploymentNamespace = "DeploymentNamespace";
        public const string NewReplicaCount = "NewReplicaCount";
    }
}
