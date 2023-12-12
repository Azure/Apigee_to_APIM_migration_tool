using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string bundlePath, string proxyName, string bearerToken, string oauthConfigName, string environment, string keyVaultName);
    }

}