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
        public XDocument LoadProxyXml(string proxyName);

        public XDocument LoadTargetXml(string targetEndpointName);

        public XDocument LoadProxyEndpointXml(string proxyEndpointName);
        public XDocument LoadPolicyXml(string policyName);
        public XDocument LoadSharedFlowBundleXml(string sharedFlowName);
        public XDocument LoadSharedFlowXml(string sharedFlowName);
        public XDocument LoadSharedFlowPolicyXml(string policyName);
    }
}
