using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ApigeeXmlFileLoader : IApigeeXmlLoader
    {
        private readonly IBundleProvider _bundleProvider;
        public ApigeeXmlFileLoader(IBundleProvider bundleProvider)
        {
            _bundleProvider = bundleProvider;
        }
        public XDocument LoadProxyXml(string proxyName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), "apiproxy", $"{proxyName}.xml"));
        }

        public XDocument LoadTargetXml(string targetEndpointName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), "apiproxy", "targets", $"{targetEndpointName}.xml"));
        }

        public XDocument LoadProxyEndpointXml(string proxyEndpointName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), "apiproxy", "proxies", $"{proxyEndpointName}.xml"));
        }

        public XDocument LoadPolicyXml(string policyName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), "apiproxy", "policies", $"{policyName}.xml"));
        }

        public XDocument LoadSharedFlowBundleXml(string sharedFlowName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), sharedFlowName, "sharedflowbundle", $"{sharedFlowName}.xml"));
        }

        public XDocument LoadSharedFlowXml(string sharedFlowName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), sharedFlowName, "sharedflowbundle", "sharedflows", $"{sharedFlowName}.xml"));
        }

        public XDocument LoadSharedFlowPolicyXml(string policyName)
        {
            return XDocument.Load(Path.Combine(_bundleProvider.GetBundlePath(), "sharedflowbundle", "policies", $"{policyName}.xml"));
        }
    }
}
