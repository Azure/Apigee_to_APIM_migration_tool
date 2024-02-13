using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service.Bundles
{
    public class ApigeeOnlineApiProxyBundle : IBundle
    {
        private readonly IApigeeManagementApiService _apigeeManagementApiService;
        private readonly string _basePath;
        private readonly string _proxyOrProductName;
        private string? _bundlePath;

        public ApigeeOnlineApiProxyBundle(string basePath, string proxyOrProductName, IApigeeManagementApiService apigeeManagementApiService)
        {
            _basePath = basePath;
            _proxyOrProductName = proxyOrProductName;
            _apigeeManagementApiService = apigeeManagementApiService;
        }

        public async Task LoadBundle()
        {
            //get api metadata
            var apiProxyMetadata = await _apigeeManagementApiService.GetApiProxyByName(_proxyOrProductName);
            //get the latest revision
            int maxRevision = apiProxyMetadata.revision.Select(x => int.Parse(x)).Max();
            //download api proxy bundle 
            _bundlePath = await _apigeeManagementApiService.DownloadApiProxyBundle(_basePath, _proxyOrProductName, maxRevision);
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
