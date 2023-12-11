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
        public Task<string> DownloadApiProxyBundle(string proxyName, int revision, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApiProductMetaData> GetApiProductByName(string productName, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApigeeEntityModel> GetApiProxyByName(string proxyName, string bearerToken)
        {
            var apiProxy = new ApigeeEntityModel
            {
                name = "test",
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

        public Task<KeyValueMapModel> GetKeyValueMapByName(string proxyName, string environment, string mapIdentifier, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApigeeTargetServerModel> GetTargetServerByName(string targetServerName, string environment, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task PopulateProxyReferenceDatabase(string bearerToken)
        {
            throw new NotImplementedException();
        }
    }
}
