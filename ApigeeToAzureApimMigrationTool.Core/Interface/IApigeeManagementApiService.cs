using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;

namespace ApigeeToAzureApimMigrationTool.Core
{
    public interface IApigeeManagementApiService
    {
        string AuthenticationToken { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        string? Environment { get; }
        string ProxyName { get; }
        Task<string> DownloadApiProxyBundle(string proxyName, int revision);
        Task<ApigeeEntityModel> GetApiProxyByName(string proxyName);
        Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision);
        Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName);
        Task PopulateProxyReferenceDatabase();
        Task<ApiProductMetaData> GetApiProductByName(string productName);
        Task<ApigeeTargetServerModel> GetTargetServerByName(string targetServerName, string environment);
        Task<KeyValueMapModel> GetKeyValueMapByName(string proxyName, string environment, string mapIdentifier);
    }
}