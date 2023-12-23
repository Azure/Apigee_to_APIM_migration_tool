using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimService : IAzureApimService
    {
        private readonly IApimProvider _apimProvider;
        private readonly IApigeeXmlLoader _apigeeXmlLoader;
        private readonly IApimPolicyTransformer _policyTransformer;


        public AzureApimService(IApimProvider apimProvider, IApigeeXmlLoader apigeeXmlLoader, IApimPolicyTransformer policyTransformer)
        {
            _apimProvider = apimProvider;
            _apigeeXmlLoader = apigeeXmlLoader;
            _policyTransformer = policyTransformer;

        }

        public async Task ImportApi(string apimName, string proxyName, string oauthConfigName, string environment, string keyVaultName)
        {
            var apiProxyXml = _apigeeXmlLoader.LoadProxyXml(proxyName);
            var apiProxyElement = apiProxyXml.Element("APIProxy");
            string apiName = apiProxyElement.Attribute("name").Value;
            string revision = apiProxyElement.Attribute("revision").Value;
            string basePath = apiProxyElement.Element("Basepaths").Value;
            string displayName = apiProxyElement.Element("DisplayName").Value;
            string description = apiProxyElement.Element("Description").Value;


            var targetEndpointElements = apiProxyElement.Element("TargetEndpoints").Elements("TargetEndpoint");
            var proxyEndpointElements = apiProxyElement.Element("ProxyEndpoints").Elements("ProxyEndpoint");
            var apiTargetEndpoint = targetEndpointElements.FirstOrDefault() == null ? "" : targetEndpointElements.FirstOrDefault().Value;

            string endpointUrl = "";
            if (!string.IsNullOrEmpty(apiTargetEndpoint))
            {
                var apiTargetXml = _apigeeXmlLoader.LoadTargetXml(apiTargetEndpoint);
                endpointUrl = !apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Descendants("URL").Any() ?
                    apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("Path").Value : apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("URL").Value;
            }

            var defaultApiProxyEndpointXml = _apigeeXmlLoader.LoadProxyEndpointXml(proxyEndpointElements.First().Value);
            string apiBasePath = defaultApiProxyEndpointXml.Root.Element("HTTPProxyConnection").Element("BasePath").Value;

            var apiResource = await _apimProvider.CreateApi(apiName, displayName, description, apimName, revision, apiBasePath, endpointUrl, oauthConfigName);

            var rawApiLevelPolicyXml = RawPolicyXml();

            // We define these explicitly, they should never be null
            XElement inboundAzureApimPolicySection = rawApiLevelPolicyXml.Element("policies")!.Element("inbound")!;
            XElement outboundAzureApimPolicySection = rawApiLevelPolicyXml.Element("policies")!.Element("outbound")!;


            string[] httpVerbs = { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

            #region Api level Policies
            var policyXmlLoader = _apigeeXmlLoader.LoadPolicyXml;

            foreach (var proxyEndpoint in proxyEndpointElements)
            {
                var apiProxyEndpointXml = _apigeeXmlLoader.LoadProxyEndpointXml(proxyEndpoint.Value);

                // get pre-flow request policies
                IEnumerable<XElement>? preFlowRequestSteps = apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(preFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName);
                
                //get post-flow request policies
                IEnumerable<XElement>? postFlowRequestSteps = apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(postFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName);

                //get pre-flow response policies
                IEnumerable<XElement>? preFlowResponseSteps = apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(preFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName);

                //get post-flow response policies
                IEnumerable<XElement>? postFlowResponseSteps = apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(postFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName);

            }

            foreach (var targetEndpoint in targetEndpointElements)
            {
                var targetEndpointXml = _apigeeXmlLoader.LoadTargetXml(targetEndpoint.Value);

                //get pre-flow request policies
                IEnumerable<XElement>? targetPreFlowRequestSteps = targetEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(targetPreFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName);

                //get post-flow request policies
                IEnumerable<XElement>? targetPostFlowRequestSteps = targetEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(targetPostFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName);

                //get pre-flow response policies
                IEnumerable<XElement>? targetPreFlowResponseSteps = targetEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(targetPreFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName);

                //get post-flow response policies
                IEnumerable<XElement>? targetPostFlowResponseSteps = targetEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(targetPostFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName);

            }

            await _apimProvider.CreatePolicy(rawApiLevelPolicyXml);
            #endregion

            #region API Operations and policies
            // create api operations
            var rawOperationLevelPolicyXml = RawPolicyXml();
            XElement inboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("inbound")!;
            XElement outboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("outbound")!;

            foreach (var proxyEndpoint in proxyEndpointElements)
            {
                var apiProxyEndpointXml = _apigeeXmlLoader.LoadProxyEndpointXml(proxyEndpoint.Value);

                var flows = apiProxyEndpointXml.Root.Element("Flows");

                if (flows.Elements("Flow").Count() > 0)
                {
                    foreach (var flow in flows.Elements("Flow"))
                    {
                        //get flow request policies
                        IEnumerable<XElement>? flowRequestSteps = flow.Element("Request")?.Elements("Step");
                        await _policyTransformer.TransformPoliciesInCollection(flowRequestSteps, inboundAzureApimOperationPolicySection, policyXmlLoader, apimName);

                        //get flow response policies
                        IEnumerable<XElement>? flowResponseSteps = flow.Element("Response")?.Elements("Step");
                        await _policyTransformer.TransformPoliciesInCollection(flowResponseSteps, outboundAzureApimOperationPolicySection, policyXmlLoader, apimName);

                        string operationName = flow.Attribute("name").Value;
                        string operationDescription = flow.Element("Description").Value;
                        string operationBasePath = apiProxyEndpointXml.Root.Element("HTTPProxyConnection").Element("BasePath").Value;
                        string condition = flow.Element("Condition").Value;
                        var successCodesElement = apiProxyEndpointXml.Root.Element("HTTPProxyConnection").Element("Properties").Elements("Property").FirstOrDefault(x => x.Attribute("name").Value == "success.codes");
                        //TODO: add response codes 
                        string[] successCodes = successCodesElement != null ? successCodesElement.Value.Split(",") : null;

                        string verbFilter = null;
                        if (condition.Contains("request.verb"))
                        {
                            string variablePattern = @"request\.verb (=|!=) ""(.*?)""";
                            foreach (Match match in Regex.Matches(condition, variablePattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                {
                                    verbFilter = $"Where (v => v {match.Groups[1].Value} \"{match.Groups[2].Value}\")";
                                }
                            }
                        }

                        string proxyPath = null;
                        if (condition.Contains("proxy.pathsuffix MatchesPath"))
                        {
                            string variablePattern = @"proxy\.pathsuffix MatchesPath ""(.*?)""";
                            foreach (Match match in Regex.Matches(condition, variablePattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                {
                                    proxyPath = match.Groups[1].Value;
                                }
                            }
                        }

                        var filteredVerbs = httpVerbs.Execute<IEnumerable<string>>(verbFilter);
                        foreach (var httpVerb in filteredVerbs)
                        {
                            await _apimProvider.CreateOrUpdateOperationPolicy(rawOperationLevelPolicyXml, operationName, operationDescription, httpVerb, proxyPath);
                        }
                    }
                }
                else
                {
                    foreach (var verb in httpVerbs)
                    {
                        await _apimProvider.CreateOrUpdateOperation(apiName, description, verb);
                    }
                }
            }
            #endregion

        }

        #region Private Methods


        private XDocument RawPolicyXml()
        {
            string rawPolicy = @"<policies>
                                    <inbound>
                                        <base />
                                    </inbound>
                                    <backend>
                                        <base />
                                    </backend>
                                    <outbound>
                                        <base />
                                    </outbound>
                                    <on-error>
                                        <base />
                                    </on-error>
                                </policies>";

            return XDocument.Parse(rawPolicy);
        }

        #endregion
    }
}