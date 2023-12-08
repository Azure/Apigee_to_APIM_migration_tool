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

        public XDocument LoadProxyXml(string path, string proxyName)
        {
            return XDocument.Load(Path.Combine(path, "apiproxy", $"{proxyName}.xml"));
        }

        public XDocument LoadTargetXml(string path, string targetEndpointName)
        {
            return XDocument.Load(Path.Combine(path, "apiproxy", "targets", $"{targetEndpointName}.xml"));
        }

        public XDocument LoadProxyEndpointXml(string path, string proxyEndpointName)
        {
            return XDocument.Load(Path.Combine(path, "apiproxy", "proxies", $"{proxyEndpointName}.xml"));
        }

        public XDocument LoadPolicyXml(string path, string policyName)
        {
            return XDocument.Load(Path.Combine(path, "apiproxy", "policies", $"{policyName}.xml"));
        }

        public XDocument LoadSharedFlowBundleXml(string path, string sharedFlowName)
        {
            return XDocument.Load(Path.Combine(path, sharedFlowName, "sharedflowbundle", $"{sharedFlowName}.xml"));
        }

        public XDocument LoadSharedFlowXml(string path, string sharedFlowName)
        {
            return XDocument.Load(Path.Combine(path, sharedFlowName, "sharedflowbundle", "sharedflows", $"{sharedFlowName}.xml"));
        }

        public XDocument LoadSharedFlowPolicyXml(string path, string policyName)
        {
            return XDocument.Load(Path.Combine(path, "sharedflowbundle", "policies", $"{policyName}.xml"));
        }
    }
}
