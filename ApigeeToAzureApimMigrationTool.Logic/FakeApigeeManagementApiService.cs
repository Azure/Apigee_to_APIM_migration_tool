using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class FakeApigeeManagementApiService : IApigeeManagementApiService
    {
        public FakeApigeeManagementApiService(string organizationName)
        {
            
        }
        public async Task<string> DownloadApiProxyBundle(string proxyName, int revision, string bearerToken)
        {
            //update with local path
            return "c:\\blah\\blah";
        }

        public Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task<ApiProductMetaData> GetApiProductByName(string productName, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public async Task<ApigeeEntityModel> GetApiProxyByName(string proxyName, string bearerToken)
        {
            return new ApigeeEntityModel
            {
                name = "fake proxy",
                revision = new string[3] { "1", "2", "3" },
                metaData = new ApiProxyMetaData()
            };
        }

        public async Task<string> GetAuthenticationToken(string oneTimeToken, string authenticationBaseUrl)
        {
            return "faketokenqwertgbxatcssD?FsDFsd";
        }

        public Task<string> GetAuthenticationToken(string username, string password, string authenticationBaseUrl)
        {
            throw new NotImplementedException();
        }

        public Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName, string bearerToken)
        {
            throw new NotImplementedException();
        }

        public Task PopulateProxyReferenceDatabase(string bearerToken)
        {
            throw new NotImplementedException();
        }
    }
}
