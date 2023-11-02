using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string apimUrl, string resourceGroupName, string bundlePath, string proxyName, string brearToken, string oauthConfigName, string backendAppId, string azureAdTenentId);
        Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string resourceGroupName, string apimName);
        Task AddApiToProduct(ApiManagementProductResource apiProductResource, string apiId);
    }

}