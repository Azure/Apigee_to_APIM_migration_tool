using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;

namespace ApigeeToAzureApimMigrationTool.Core
{
    public interface IApigeeManagementApiService
    {
        Task<string> DownloadApiProxyBundle(string proxyName, int revision, string bearerToken);
        Task<ApigeeEntityModel> GetApiProxyByName(string proxyName, string bearerToken);
        Task<string> GetAuthenticationToken(string oneTimeToken, string authenticationBaseUrl);
        Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision, string bearerToken);
        Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName, string bearerToken);
        Task PopulateProxyReferenceDatabase(string bearerToken);
        Task<string> GetAuthenticationToken(string username, string password, string authenticationBaseUrl);

        Task<ApiProductMetaData> GetApiProductByName(string productName, string bearerToken);
    }
}