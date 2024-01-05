using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Test
{
    public class MockApigeeManagementApiService : IApigeeManagementApiService
    {

        public string AuthenticationToken { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string? Environment { get; private set; }

        public string ProxyName { get; set; }

        public Task<string> DownloadApiProxyBundle(string proxyName, int revision)

        {
            return Task.FromResult(string.Empty);
        }

        public Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<ApiProductMetaData> GetApiProductByName(string productName)
        {
            var apiProduct = new ApiProductMetaData
            {
                Name = productName,
                DisplayName = productName,
                Description = "test",
                Proxies = new string[] { "test" }
            };

            return Task.FromResult(apiProduct);
        }

        public Task<ApigeeEntityModel> GetApiProxyByName(string proxyName)
        {
            var apiProxy = new ApigeeEntityModel
            {
                name = proxyName,
                revision = new string[] { "1" },
                metaData = new ApiProxyMetaData
                {
                    createdAt = 0,
                    createdBy = "test",
                    lastModifiedAt = 0,
                    lastModifiedBy = "test",
                    subType = "test"
                },
            };

            return Task.FromResult(apiProxy);
        }

        public Task<string> GetAuthenticationToken(string oneTimeToken, string authenticationBaseUrl)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<string> GetAuthenticationToken(string username, string password, string authenticationBaseUrl)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<KeyValueMapModel> GetKeyValueMapByName(string proxyName, string environment, string mapIdentifier)
        {
            var keyMap = new KeyValueMapModel
            {
                Name = mapIdentifier,
                Encrypted = false,
                Entry = new KeyValueMapItemModel[]
                {
                    new KeyValueMapItemModel
                    {
                        Name = "test",
                        Value = "test"
                    }
                }
            };

            return Task.FromResult(keyMap);

        }

        public Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName)
        {
            var sharedFlow = new ApigeeEntityModel { name = sharedFlowName, revision = new string[] { "1" } };
            return Task.FromResult(sharedFlow);
        }

        public Task<ApigeeTargetServerModel> GetTargetServerByName(string targetServerName, string environment)
        {
            var targetServer = new ApigeeTargetServerModel
            {
                Name = targetServerName,
                Host = "test",
                Port = 443,
                IsEnabled = true,
                SSLInfo = new ApigeeTargetServerSSLInfo
                {
                    Ciphers = new string[] { "test" },
                    ClientAuthEnabled = true,
                    Enabled = true,
                    IgnoreValidationError = true,
                    Protocols = new string[] { "test" },
                    TrustStore = "test"
                }
            };

            return Task.FromResult(targetServer);
        }

        public Task PopulateProxyReferenceDatabase()
        {
            return Task.CompletedTask;
        }
    }
}
