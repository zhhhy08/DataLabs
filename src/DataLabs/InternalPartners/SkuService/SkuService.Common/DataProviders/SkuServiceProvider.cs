namespace SkuService.Common.DataProviders
{
    public class SkuServiceProvider: ISkuServiceProvider
    {
        private readonly string serviceName;

        public SkuServiceProvider(string serviceName)
        {
            this.serviceName = serviceName;
        }

        public string GetServiceName()
        {
            return this.serviceName;
        }
    }
}
