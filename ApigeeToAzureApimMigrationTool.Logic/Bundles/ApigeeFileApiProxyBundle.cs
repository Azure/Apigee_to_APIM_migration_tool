using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service.Bundles
{
    public class ApigeeFileApiProxyBundle : IBundle
    {
        private string _bundleBasePath;
        private string _proxyOrProductName;

        public ApigeeFileApiProxyBundle(string bundleBasePath, string proxyOrProductName)
        {
            _bundleBasePath = bundleBasePath;
            _proxyOrProductName = proxyOrProductName;
        }
        
        public string GetBundlePath()
        {
            if (string.IsNullOrEmpty(_proxyOrProductName))
            {
                throw new Exception("API Proxy bundle not loaded. Please load the bundle first");
            }

            return Path.Combine(_bundleBasePath, _proxyOrProductName, "apiproxy");
        }

        public Task LoadBundle()
        {
            // Nothing to return, the bundle is already in the filesystem
            return Task.CompletedTask;
        }
    }
}
