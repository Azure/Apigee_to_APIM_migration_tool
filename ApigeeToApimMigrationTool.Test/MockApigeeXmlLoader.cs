using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToApimMigrationTool.Test
{
    public class MockApigeeXmlLoader : IApigeeXmlLoader
    {
        public Dictionary<string, XDocument> PolicyXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> ProxyEndpointXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> ProxyXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> SharedFlowBundleXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> SharedFlowPolicyXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> SharedFlowXml { get; set; } = new Dictionary<string, XDocument>();
        public Dictionary<string, XDocument> TargetXml { get; set; } = new Dictionary<string, XDocument>();

        public XDocument LoadPolicyXml(string proxyName, string policyName)
        {
            if (PolicyXml.ContainsKey(policyName))
            {
                return PolicyXml[policyName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadProxyEndpointXml(string proxyName, string proxyEndpointName)
        {
            if (ProxyEndpointXml.ContainsKey(proxyEndpointName))
            {
                return ProxyEndpointXml[proxyEndpointName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadProxyXml(string proxyName)
        {
            if (ProxyXml.ContainsKey(proxyName))
            {
                return ProxyXml[proxyName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadSharedFlowBundleXml(string sharedFlowName)
        {
            if (SharedFlowBundleXml.ContainsKey(sharedFlowName))
            {
                return SharedFlowBundleXml[sharedFlowName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadSharedFlowPolicyXml(string sharedFlowName, string policyName)
        {
            if (SharedFlowPolicyXml.ContainsKey(policyName))
            {
                return SharedFlowPolicyXml[policyName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadSharedFlowXml(string sharedFlowName, string sharedFlowFileName)
        {
            if (SharedFlowXml.ContainsKey(sharedFlowName))
            {
                return SharedFlowXml[sharedFlowName];
            }
            else
            {
                return new XDocument();
            }
        }

        public XDocument LoadTargetXml(string proxyName, string targetEndpointName)
        {
            if (TargetXml.ContainsKey(targetEndpointName))
            {
                return TargetXml[targetEndpointName];
            }
            else
            {
                return new XDocument();
            }
        }
    }
}
