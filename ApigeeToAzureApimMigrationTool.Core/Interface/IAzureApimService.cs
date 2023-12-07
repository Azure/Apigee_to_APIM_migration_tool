using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string apimUrl, string resourceGroupName, string bundlePath, string proxyName, string brearToken,
            string oauthConfigName, string backendAppId, string azureAdTenentId, string environment, string keyVaultName);
        Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string resourceGroupName, string apimName);
        Task AddApiToProduct(ApiManagementProductResource apiProductResource, string apiId);
    }

}