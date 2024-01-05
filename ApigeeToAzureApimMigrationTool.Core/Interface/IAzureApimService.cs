using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string proxyName, string oauthConfigName, string environment, string keyVaultName);
    }

}