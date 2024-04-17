using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service.Bundles
{
    public class ApigeeOnlineSharedFlowBundle : IBundle
    {
        private readonly IApigeeManagementApiService _apigeeManagementApiService;
        private string _bundleBasePath;
        private string _sharedFlowName;
        private string? _bundlePath;

        public ApigeeOnlineSharedFlowBundle(string bundleBasePath, string sharedFlowName, IApigeeManagementApiService apigeeManagementApiService)
        {
            _bundleBasePath = bundleBasePath;
            _sharedFlowName = sharedFlowName;
            _apigeeManagementApiService = apigeeManagementApiService;
        }

        public async Task LoadBundle()
        {
            //get api metadata
            var sharedFlowMetadata = await _apigeeManagementApiService.GetSharedFlowByName(_sharedFlowName);
            //get the latest revision
            int maxRevision = sharedFlowMetadata.revision.Select(x => int.Parse(x)).Max();
            //download api proxy bundle 
            _bundlePath = await _apigeeManagementApiService.DownloadSharedFlowBundle(_bundleBasePath, _sharedFlowName, maxRevision);
        }
        public string GetBundlePath()
        {
            if (string.IsNullOrEmpty(_bundlePath))
            {
                throw new Exception("Shared flow bundle not loaded. Please load the bundle first");
            }

            return Path.Combine(_bundleBasePath, _sharedFlowName, "sharedflowbundle");

        }
    }
}
