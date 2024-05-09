namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis;
    using Newtonsoft.Json;
    using Swashbuckle.AspNetCore.Annotations;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using k8s.Models;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants;

    [ApiController]
    [Route("admin/datalabsoperations/kubernetes/[action]")]
    public class AdminKubernetesController : ControllerBase
    {
        #region Tracing

        private static ActivityMonitorFactory AdminKubernetesControllerGetPods = 
            new("AdminKubernetesController.GetPods");
        private static ActivityMonitorFactory AdminKubernetesControllerGetAllPods =
            new("AdminKubernetesController.GetAllPods");
        private static ActivityMonitorFactory AdminKubernetesControllerGetAllConfigMaps =
            new("AdminKubernetesController.GetAllConfigMaps");
        private static ActivityMonitorFactory AdminKubernetesControllerGetDeployment =
            new("AdminKubernetesController.GetDeployment");
        private static ActivityMonitorFactory AdminKubernetesControllerGetDaemonSet =
            new("AdminKubernetesController.GetDaemonSet");
        private static ActivityMonitorFactory AdminKubernetesControllerGetPodLogs =
            new("AdminKubernetesController.GetPodLogs");
        private static ActivityMonitorFactory AdminKubernetesControllerUpdateMultipleConfigMapKeys =
            new("AdminKubernetesController.UpdateMultipleConfigMapKeys");
        private static ActivityMonitorFactory AdminKubernetesControllerUpdateConfigMapKey =
            new("AdminKubernetesController.UpdateConfigMapKey");
        private static ActivityMonitorFactory AdminKubernetesControllerDeletePod =
            new("AdminKubernetesController.DeletePod");
        private static ActivityMonitorFactory AdminKubernetesControllerScaleDeploymentReplicaCount =
            new("AdminKubernetesController.ScaleDeploymentReplicaCount");
        private static ActivityMonitorFactory AdminKubernetesControllerRestartDeployment =
            new("AdminKubernetesController.RestartDeployment");
        private static ActivityMonitorFactory AdminKubernetesControllerRestartDaemonSet =
            new("AdminKubernetesController.RestartDaemonSet");

        #endregion

        #region Startup

        private IKubernetesProvider _kubernetesProvider;
        private IKubernetesObjectTransformUtils _kubernetesObjectTransformUtils;

        public AdminKubernetesController(IKubernetesProvider kubernetesProvider, IKubernetesObjectTransformUtils kubernetesObjectTransformUtils)
        {
            _kubernetesProvider = kubernetesProvider;
            _kubernetesObjectTransformUtils = kubernetesObjectTransformUtils;
        }

        #endregion

        #region Actions

        #region Getters

        [HttpGet]
        [ActionName(AdminConstants.GetPods)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.ReadOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<IList<object>>> GetPods(
            [FromQuery(Name = AdminConstants.PodNamespace)] string podNamespace)
        {
            using var monitor = AdminKubernetesControllerGetPods.ToMonitor();
            try
            {
                monitor.OnStart();
                monitor.Activity[AdminConstants.PodNamespace] = podNamespace;

                var client = _kubernetesProvider.GetKubernetesClient();
                var podList = await client.ListNamespacedPodAsync(podNamespace);
                var reducedPodInfo = _kubernetesObjectTransformUtils.SimplifyPodListOutput(podList);

                monitor.OnCompleted();
                return Ok(reducedPodInfo);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpGet]
        [ActionName(AdminConstants.GetAllPods)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.ReadOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<IList<object>>> GetAllPods()
        {
            using var monitor = AdminKubernetesControllerGetAllPods.ToMonitor();
            try
            {
                monitor.OnStart();
                var client = _kubernetesProvider.GetKubernetesClient();
                var podList = await client.ListPodForAllNamespacesAsync();
                var reducedPodInfo = _kubernetesObjectTransformUtils.SimplifyPodListOutput(podList);

                monitor.OnCompleted();
                return Ok(reducedPodInfo);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpGet]
        [ActionName(AdminConstants.GetAllConfigMaps)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.ReadOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<IList<object>>> GetAllConfigMaps()
        {
            using var monitor = AdminKubernetesControllerGetAllConfigMaps.ToMonitor();
            try
            {
                monitor.OnStart();
                var client = _kubernetesProvider.GetKubernetesClient();
                var configMapList = await client.ListConfigMapForAllNamespacesAsync();
                var reducedConfigMapInfo = _kubernetesObjectTransformUtils.SimplifyConfigMapOutput(configMapList);

                monitor.OnCompleted();
                return Ok(reducedConfigMapInfo);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpGet]
        [ActionName(AdminConstants.GetPodLogs)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.ReadOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> GetPodLogs(
            [Required][FromQuery(Name = AdminConstants.PodNamespace)] string podNamespace,
            [Required][FromQuery(Name = AdminConstants.PodName)] string podName,
            [FromQuery(Name = AdminConstants.ContainerName)] string? containerName = null)
        {
            using var monitor = AdminKubernetesControllerGetPodLogs.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.PodNamespace] = podNamespace;
                monitor.Activity[AdminConstants.PodName] = podName;
                monitor.Activity[AdminConstants.ContainerName] = containerName;
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();
                var res = await client.ReadNamespacedPodLogAsync(podNamespace, podName, containerName);

                monitor.OnCompleted();
                return Ok(new FileStreamResult(res, "application/octet-stream"));
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpGet]
        [ActionName(AdminConstants.GetDeployment)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<V1Deployment>> GetDeployment(
            [FromQuery(Name = AdminConstants.DeploymentName)] string deploymentName,
            [FromQuery(Name = AdminConstants.DeploymentNamespace)] string deploymentNamespace)
        {
            using var monitor = AdminKubernetesControllerGetDeployment.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.DeploymentName] = deploymentName;
                monitor.Activity[AdminConstants.DeploymentNamespace] = deploymentNamespace;
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();

                var res = await client.ReadNamespacedDeploymentAsync(deploymentName, deploymentNamespace);

                monitor.OnCompleted();
                return Ok(res);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpGet]
        [ActionName(AdminConstants.GetDaemonSet)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<V1DaemonSet>> GetDaemonSet(
            [FromQuery(Name = AdminConstants.DaemonSetName)] string daemonSetName,
            [FromQuery(Name = AdminConstants.DaemonSetNamespace)] string daemonSetNamespace)
        {
            using var monitor = AdminKubernetesControllerGetDaemonSet.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.DaemonSetName] = daemonSetName;
                monitor.Activity[AdminConstants.DaemonSetNamespace] = daemonSetNamespace;
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();

                var res = await client.ReadNamespacedDaemonSetAsync(daemonSetName, daemonSetNamespace);

                monitor.OnCompleted();
                return Ok(res);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        #endregion

        #region Setters

        [HttpPatch]
        [ActionName(AdminConstants.UpdateConfigMapKey)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<V1ConfigMap>> UpdateConfigMapKey(
            [Required][FromQuery(Name = AdminConstants.ConfigMapNamespace)] string configMapNamespace,
            [Required][FromQuery(Name = AdminConstants.ConfigMapName)] string configMapName,
            [Required][FromQuery(Name = AdminConstants.ConfigMapKey)] string configMapKey,
            [Required][FromQuery(Name = AdminConstants.ConfigMapValue)] string configMapValue
            )
        {
            using var monitor = AdminKubernetesControllerUpdateConfigMapKey.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.ConfigMapName] = configMapName;
                monitor.Activity[AdminConstants.ConfigMapNamespace] = configMapNamespace;
                monitor.Activity[AdminConstants.ConfigMapKey] = configMapKey;
                monitor.Activity[AdminConstants.ConfigMapValue] = configMapValue;
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();

                var configMapOverrideJson = new
                {
                    data = new Dictionary<string, string>
                    {
                        [configMapKey] = configMapValue
                    }
                };
                var configMapOverridePatch = new V1Patch(configMapOverrideJson, V1Patch.PatchType.MergePatch);
                var res = await client.PatchNamespacedConfigMapAsync(configMapOverridePatch, configMapName, configMapNamespace);

                monitor.OnCompleted();
                return Ok(res);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.UpdateMultipleConfigMapKeys)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<V1ConfigMap>> UpdateMultipleConfigMapKeys(
            [Required][FromQuery(Name = AdminConstants.ConfigMapNamespace)] string configMapNamespace,
            [Required][FromQuery(Name = AdminConstants.ConfigMapName)] string configMapName,
            [Required, FromBody][FromQuery(Name = AdminConstants.ConfigMapOverride)] Dictionary<string, string> configMapOverride
            )
        {
            using var monitor = AdminKubernetesControllerUpdateMultipleConfigMapKeys.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.ConfigMapName] = configMapName;
                monitor.Activity[AdminConstants.ConfigMapNamespace] = configMapNamespace;
                monitor.Activity[AdminConstants.ConfigMapOverride+"keys"] = string.Join(", ", configMapOverride.Select(kvp => kvp.ToString()));
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();

                var configMapOverrideJson = new
                {
                    data = configMapOverride
                };
                var configMapOverridePatch = new V1Patch(configMapOverrideJson, V1Patch.PatchType.MergePatch);
                var result = await client.PatchNamespacedConfigMapAsync(
                    configMapOverridePatch, 
                    configMapName, 
                    configMapNamespace);

                monitor.OnCompleted();
                return Ok(result);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        #endregion

        #region kube resource management

        [HttpPatch]
        [ActionName(AdminConstants.DeletePod)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> DeletePod(
            [Required][FromQuery(Name = AdminConstants.PodNamespace)] string podNamespace,
            [Required][FromQuery(Name = AdminConstants.PodName)] string podName)
        {
            using var monitor = AdminKubernetesControllerDeletePod.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.PodName] = podName;
                monitor.Activity[AdminConstants.PodNamespace] = podNamespace;
                monitor.OnStart();

                var client = _kubernetesProvider.GetKubernetesClient();
                var res = await client.DeleteNamespacedPodAsync(podName, podNamespace);

                monitor.OnCompleted();
                return Ok($"{res.Name()} ({res.Namespace()}) is deleted");
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.ScaleDeploymentReplicaCount)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> ScaleDeploymentReplicaCount(
            [Required][FromQuery(Name = AdminConstants.DeploymentNamespace)] string deploymentNamespace,
            [Required][FromQuery(Name = AdminConstants.DeploymentName)] string deploymentName,
            [Required][FromQuery(Name = AdminConstants.NewReplicaCount)] int newReplicaCount)
        {
            using var monitor = AdminKubernetesControllerScaleDeploymentReplicaCount.ToMonitor();
            if (newReplicaCount < 0)
            {
                return BadRequest("replicaCount cannot be < 0");
            }
            try
            {
                monitor.Activity[AdminConstants.DeploymentNamespace] = deploymentNamespace;
                monitor.Activity[AdminConstants.DeploymentName] = deploymentName;

                var client = _kubernetesProvider.GetKubernetesClient();

                var deployment = await client.ReadNamespacedDeploymentAsync(deploymentName, deploymentNamespace).ConfigureAwait(false);
                deployment = _kubernetesObjectTransformUtils.ScaleDeploymentReplicasObject(deployment, newReplicaCount);
                var result = await client.ReplaceNamespacedDeploymentAsync(deployment, deploymentName, deploymentNamespace).ConfigureAwait(false);

                monitor.Activity[AdminConstants.NewReplicaCount] = newReplicaCount;
                monitor.OnCompleted();
                return Ok($"{AdminConstants.NewReplicaCount}: {newReplicaCount}");
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        #region Deployment/Daemonset Related

        [HttpPatch]
        [ActionName(AdminConstants.RestartDeployment)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> RestartDeployment(
            [Required][FromQuery(Name = AdminConstants.DeploymentNamespace)] string deploymentNamespace,
            [Required][FromQuery(Name = AdminConstants.DeploymentName)] string deploymentName)
        {
            using var monitor = AdminKubernetesControllerRestartDeployment.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.DeploymentNamespace] = deploymentNamespace;
                monitor.Activity[AdminConstants.DeploymentName] = deploymentName;

                var client = _kubernetesProvider.GetKubernetesClient();

                var deployment = await client.ReadNamespacedDeploymentAsync(deploymentName, deploymentNamespace).ConfigureAwait(false);
                deployment = _kubernetesObjectTransformUtils.UpdateDeploymentObjectRestart(deployment);

                // When Deployment DateTime is Updated it automatically triggers a rollout restart in C# Client
                var result = await client.ReplaceNamespacedDeploymentAsync(deployment, deploymentName, deploymentNamespace).ConfigureAwait(false);

                monitor.Activity["deploymentRestarted"] = true;
                monitor.OnCompleted();
                return Ok($"Restarted Deployment");
            } 
            catch (Exception ex)
            {
                monitor.Activity["deploymentRestarted"] = false;
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.RestartDaemonSet)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> RestartDaemonset(
            [Required][FromQuery(Name = AdminConstants.DaemonSetNamespace)] string daemonSetNamespace,
            [Required][FromQuery(Name = AdminConstants.DaemonSetName)] string daemonSetName)
        {
            using var monitor = AdminKubernetesControllerRestartDaemonSet.ToMonitor();
            try
            {
                monitor.Activity[AdminConstants.DaemonSetNamespace] = daemonSetNamespace;
                monitor.Activity[AdminConstants.DaemonSetName] = daemonSetName;

                var client = _kubernetesProvider.GetKubernetesClient();

                var daemonSet = await client.ReadNamespacedDaemonSetAsync(daemonSetName, daemonSetNamespace).ConfigureAwait(false);
                daemonSet = _kubernetesObjectTransformUtils.UpdateDaemonSetObjectRestart(daemonSet);

                // When daemonSet DateTime is Updated it automatically triggers a rollout restart in C# Client
                var result = await client.ReplaceNamespacedDaemonSetAsync(daemonSet, daemonSetName, daemonSetNamespace).ConfigureAwait(false);
               
                monitor.Activity["daemonSetRestarted"] = true;
                monitor.OnCompleted();
                return Ok($"Restarted DaemonSet");
            }
            catch (Exception ex)
            {
                monitor.Activity["daemonSetRestarted"] = false;
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        #endregion

        #endregion

        #endregion
    }
}
