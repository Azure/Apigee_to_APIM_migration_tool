using Azure.ResourceManager.ApiManagement;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IAzureApimService
    {
        Task ImportApi(string apimName, string apimUrl, string resourceGroupName, string bundlePath, string proxyName, string brearToken, string oauthConfigName, string backendAppId, string azureAdTenentId);
        Task AddApiToProduct(ApiManagementProductResource apiProductResource, string apiId);
    }

}