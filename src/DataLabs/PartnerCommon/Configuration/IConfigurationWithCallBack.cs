namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    public interface IConfigurationWithCallBack : IConfiguration
    {
        public T? GetValueWithCallBack<T>(string key, Func<T, Task> callback, T? defaultValue = default, bool allowMultiCallBacks = true) where T : notnull;
    }
}
