using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ApigeeFileSharedFlowBundle : IBundle
    {
        private string _bundleBasePath;
        private string _sharedFlowName;

        public ApigeeFileSharedFlowBundle(string bundleBasePath, string sharedFlowName)
        {
            _bundleBasePath = bundleBasePath;
            _sharedFlowName = sharedFlowName;
        }

        public string GetBundlePath()
        {
            if (string.IsNullOrEmpty(_sharedFlowName))
            {
                throw new Exception("Shared flow bundle not loaded. Please load the bundle first");
            }

            return Path.Combine(_bundleBasePath, _sharedFlowName, "sharedflowbundle");
        }

        public Task LoadBundle()
        {   
            // Nothing to return, the bundle is already in the filesystem
            return Task.CompletedTask;
        }
    }
}
