using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IApigeeXmlLoader
    {
        public XDocument LoadProxyXml(string path, string proxyName);

        public XDocument LoadTargetXml(string path, string targetEndpointName);

        public XDocument LoadProxyEndpointXml(string path, string proxyEndpointName);
        public XDocument LoadPolicyXml(string path, string policyName);
        public XDocument LoadSharedFlowBundleXml(string path, string sharedFlowName);
        public XDocument LoadSharedFlowXml(string path, string sharedFlowName);
        public XDocument LoadSharedFlowPolicyXml(string path, string policyName);
    }
}
