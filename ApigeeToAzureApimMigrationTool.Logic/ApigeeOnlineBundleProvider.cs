using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ApigeeOnlineBundleProvider : IBundleProvider
    {
        private readonly IApigeeManagementApiService _apigeeManagementApiService;
        private string? _bundlePath;

        public ApigeeOnlineBundleProvider(IApigeeManagementApiService apigeeManagementApiService)
        {
            _apigeeManagementApiService = apigeeManagementApiService;
        }

        public async Task LoadBundle(string proxyOrProductName)
        {
            //get api metadata
            var apiProxyMetadata = await _apigeeManagementApiService.GetApiProxyByName(proxyOrProductName);
            //get the latest revision
            int maxRevision = apiProxyMetadata.revision.Select(x => int.Parse(x)).Max();
            //download api proxy bundle 
            _bundlePath = await _apigeeManagementApiService.DownloadApiProxyBundle(proxyOrProductName, maxRevision);
        }
        public string GetBundlePath()
        {
            if (string.IsNullOrEmpty(_bundlePath))
            {
                throw new Exception("Bundle not loaded. Please load the bundle first");
            }

            return _bundlePath;
        }
    }
}
