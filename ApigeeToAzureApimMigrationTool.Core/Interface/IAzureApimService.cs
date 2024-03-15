using ApigeeToApimMigrationTool.Core.Config;
using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string proxyName, ApimConfiguration apimConfiguration, ApigeeConfiguration apigeeConfiguration, string keyVaultName);

    }
}