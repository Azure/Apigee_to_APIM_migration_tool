using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimService : IAzureApimService
    {
        private readonly IApimProvider _apimProvider;
        private readonly IApigeeXmlLoader _apigeeXmlLoader;
        private readonly IApimPolicyTransformer _policyTransformer;
        private readonly IExpressionTranslator _expressionTranslator;

        public AzureApimService(IApimProvider apimProvider, IApigeeXmlLoader apigeeXmlLoader, IApimPolicyTransformer policyTransformer, IExpressionTranslator expressionTranslator)
        {
            _apimProvider = apimProvider;
            _apigeeXmlLoader = apigeeXmlLoader;
            _policyTransformer = policyTransformer;
            _expressionTranslator = expressionTranslator;
        }

        public async Task ImportApi(string apimName, string proxyName, ApimConfiguration apimConfiguration, ApigeeConfiguration apigeeConfiguration, string keyVaultName)
        {
            var apiProxyXml = _apigeeXmlLoader.LoadProxyXml(proxyName);
            var apiProxyElement = apiProxyXml.Element("APIProxy");
            string apiName = apiProxyElement.Attribute("name").Value;
            string revision = apiProxyElement.Attribute("revision").Value;
            //string basePath = apiProxyElement.Element("Basepaths").Value;
            string displayName = apiProxyElement.Element("DisplayName").Value;
            string description = apiProxyElement.Element("Description").Value;


            var targetEndpointElements = apiProxyElement.Element("TargetEndpoints").Elements("TargetEndpoint");
            var proxyEndpointElement = apiProxyElement.Element("ProxyEndpoints");

            foreach (var proxyEndpoint in proxyEndpointElement.Elements("ProxyEndpoint"))
            {
                string proxyEndpointName = proxyEndpoint.Value;
                var apiProxyEndpointXml = _apigeeXmlLoader.LoadProxyEndpointXml(proxyName, proxyEndpointName);


                //get the first one for now
                var apiTargetEndpoint = apiProxyEndpointXml.Root.Elements("RouteRule")?.First(x => x.Element("TargetEndpoint") != null)?.Element("TargetEndpoint").Value;

                string endpointUrl = "";
                if (!string.IsNullOrEmpty(apiTargetEndpoint))
                {
                    var apiTargetXml = _apigeeXmlLoader.LoadTargetXml(proxyName, apiTargetEndpoint);
                    endpointUrl = !apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Descendants("URL").Any() ?
                        apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("Path").Value : apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("URL").Value;
                }

                string apiBasePath = apiProxyEndpointXml.Root.Element("HTTPProxyConnection")?.Element("BasePath")?.Value;

                //79 because we'll append the proxy endpoint name with an underscore 
                string? apiNameToUse = null;
                if (apiName.Length + proxyEndpointName.Length > 79)
                    apiNameToUse = apiName.Substring(0, 79 - proxyEndpointName.Length);
                else
                    apiNameToUse = apiName;

                apiNameToUse = $"{apiNameToUse}_{proxyEndpointName}";

                string? displayNameToUse = null;
                if (displayName.Length + proxyEndpointName.Length > 79)
                    displayNameToUse = displayName.Substring(0, 79 - proxyEndpointName.Length);
                else
                    displayNameToUse = displayName;

                displayNameToUse = $"{displayNameToUse}_{proxyEndpointName}";


                await _apimProvider.CreateApi(apiNameToUse, displayNameToUse, description, apimName, revision, apiBasePath, endpointUrl, apimConfiguration.OAuthConfigName);

                var rawApiLevelPolicyXml = RawPolicyXml();
                // We define these explicitly, they should never be null
                XElement inboundAzureApimPolicySection = rawApiLevelPolicyXml.Element("policies")!.Element("inbound")!;
                XElement outboundAzureApimPolicySection = rawApiLevelPolicyXml.Element("policies")!.Element("outbound")!;


                string[] httpVerbs = { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

                #region Api level Policies
                var policyXmlLoader = _apigeeXmlLoader.LoadPolicyXml;

                // get pre-flow request policies
                IEnumerable<XElement>? preFlowRequestSteps = apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(preFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                //get post-flow request policies
                IEnumerable<XElement>? postFlowRequestSteps = apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(postFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                //get pre-flow response policies
                IEnumerable<XElement>? preFlowResponseSteps = apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(preFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                //get post-flow response policies
                IEnumerable<XElement>? postFlowResponseSteps = apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step");
                await _policyTransformer.TransformPoliciesInCollection(postFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                // target endpoint policies will only be applied if there is one target endpoint defined for the proxy endpoint. if there are multiple 
                // target endpoints with conditions then policies won't be moved and user must migrate them manually
                if (apiProxyEndpointXml.Root.Elements("RouteRule").Where(x => x.Element("TargetEndpoint") != null).Count() == 1)
                {
                    var targetEndpointXml = _apigeeXmlLoader.LoadTargetXml(proxyName, apiTargetEndpoint);

                    //get pre-flow request policies
                    IEnumerable<XElement>? targetPreFlowRequestSteps = targetEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step");
                    await _policyTransformer.TransformPoliciesInCollection(targetPreFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                    //get post-flow request policies
                    IEnumerable<XElement>? targetPostFlowRequestSteps = targetEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step");
                    await _policyTransformer.TransformPoliciesInCollection(targetPostFlowRequestSteps, inboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                    //get pre-flow response policies
                    IEnumerable<XElement>? targetPreFlowResponseSteps = targetEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step");
                    await _policyTransformer.TransformPoliciesInCollection(targetPreFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                    //get post-flow response policies
                    IEnumerable<XElement>? targetPostFlowResponseSteps = targetEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step");
                    await _policyTransformer.TransformPoliciesInCollection(targetPostFlowResponseSteps, outboundAzureApimPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);
                }
                else
                {
                    foreach (var routeRule in apiProxyEndpointXml.Root.Elements("RouteRule"))
                    {
                        var targetEndpoint = routeRule.Element("TargetEndpoint")?.Value;

                        if (string.IsNullOrEmpty(targetEndpoint))
                            continue;

                        var targetEndpointXml = _apigeeXmlLoader.LoadTargetXml(proxyName, targetEndpoint);
                        var targetEndpointPath = !targetEndpointXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Descendants("URL").Any() ?
                        targetEndpointXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("Path").Value : targetEndpointXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("URL").Value;

                        var tergetEndpointCondition = routeRule.Element("Condition")?.Value;

                        if (string.IsNullOrEmpty(tergetEndpointCondition))
                            continue;

                        //set backend for the operation based on the condition ofthe target backend.
                        if (tergetEndpointCondition != null && tergetEndpointCondition.Contains("request.verb"))
                            tergetEndpointCondition = tergetEndpointCondition.Replace("request.verb", "context.Operation.Method");

                        if (tergetEndpointCondition != null && tergetEndpointCondition.Contains("proxy.pathsuffix MatchesPath"))
                        {
                            string matchesPathPattern = @"proxy\.pathsuffix MatchesPath ""(.*?)""";
                            foreach (Match match in Regex.Matches(tergetEndpointCondition, matchesPathPattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                    tergetEndpointCondition = tergetEndpointCondition.Replace(match.Groups[0].Value,
                                        $"context.Request.OriginalUrl.Path.ToString().Replace(\"{apiBasePath}\",\"\") == \"{match.Groups[1].Value.Replace("**", "").Replace("*", "")}\"");
                            }
                        }

                        if (tergetEndpointCondition != null && tergetEndpointCondition.Contains("proxy.pathsuffix Matches"))
                        {
                            string matchesPattern = @"proxy\.pathsuffix Matches ""(.*?)""";
                            foreach (Match match in Regex.Matches(tergetEndpointCondition, matchesPattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                    tergetEndpointCondition = tergetEndpointCondition.Replace(match.Groups[0].Value,
                                        $"context.Request.OriginalUrl.Path.ToString().Replace(\"{apiBasePath}\",\"\") == \"{match.Groups[1].Value.Replace("**", "").Replace("*", "")}\"");
                            }
                        }

                        tergetEndpointCondition = "@(" + HttpUtility.HtmlDecode(_expressionTranslator.TranslateConditionOperator(tergetEndpointCondition)) + ")";
                        if (targetEndpointPath.StartsWith("/"))
                            targetEndpointPath = apimConfiguration.Url + targetEndpointPath;
                        XElement setBackend = new XElement("set-backend-service", new XAttribute("base-url", targetEndpointPath));
                        XElement choose = new XElement("choose");
                        XElement when = new XElement("when", new XAttribute("condition", tergetEndpointCondition));
                        when.Add(setBackend);
                        choose.Add(when);
                        inboundAzureApimPolicySection.Add(choose);
                    }
                }

                await _apimProvider.CreatePolicy(rawApiLevelPolicyXml);
                #endregion

                #region API Operations and policies
                // create api operations
                var rawOperationLevelPolicyXml = RawPolicyXml();
                XElement inboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("inbound")!;
                XElement outboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("outbound")!;

                var flows = apiProxyEndpointXml.Root.Element("Flows");

                if (flows.Elements("Flow").Count() > 0)
                {
                    foreach (var flow in flows.Elements("Flow"))
                    {
                        //get flow request policies
                        IEnumerable<XElement>? flowRequestSteps = flow.Element("Request")?.Elements("Step");
                        await _policyTransformer.TransformPoliciesInCollection(flowRequestSteps, inboundAzureApimOperationPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                        //get flow response policies
                        IEnumerable<XElement>? flowResponseSteps = flow.Element("Response")?.Elements("Step");
                        await _policyTransformer.TransformPoliciesInCollection(flowResponseSteps, outboundAzureApimOperationPolicySection, policyXmlLoader, apimName, proxyName, apigeeConfiguration, apimConfiguration);

                        string operationName = flow.Attribute("name").Value;
                        string operationDescription = flow.Element("Description").Value;
                        string operationBasePath = apiProxyEndpointXml.Root.Element("HTTPProxyConnection")?.Element("BasePath")?.Value;
                        string condition = flow.Element("Condition")?.Value;
                        var successCodesElement = apiProxyEndpointXml.Root.Element("HTTPProxyConnection")?.Element("Properties")?.Elements("Property")?.FirstOrDefault(x => x.Attribute("name").Value == "success.codes");
                        //TODO: add response codes 
                        string[] successCodes = successCodesElement != null ? successCodesElement.Value.Split(",") : null;

                        string verbFilter = null;
                        if (condition != null && condition.Contains("request.verb"))
                        {
                            string variablePattern = @"request\.verb (=|!=) ""(.*?)""";
                            foreach (Match match in Regex.Matches(condition, variablePattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                {
                                    verbFilter = $"Where (v => v {_expressionTranslator.TranslateConditionOperator(" " + match.Groups[1].Value + " ")} \"{match.Groups[2].Value}\")";
                                }
                            }
                        }

                        string proxyPath = null;
                        if (condition != null && condition.Contains("proxy.pathsuffix MatchesPath"))
                        {
                            string variablePattern = @"proxy\.pathsuffix MatchesPath ""(.*?)""";
                            foreach (Match match in Regex.Matches(condition, variablePattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                {
                                    proxyPath = match.Groups[1].Value.Replace("**", "").Replace("*", ""); ;
                                }
                            }
                        }

                        if (condition != null && condition.Contains("proxy.pathsuffix Matches"))
                        {
                            string variablePattern = @"proxy\.pathsuffix Matches ""(.*?)""";
                            foreach (Match match in Regex.Matches(condition, variablePattern))
                            {
                                if (match.Success && match.Groups.Count > 0)
                                {
                                    proxyPath = match.Groups[1].Value.Replace("**", "").Replace("*", "");
                                }
                            }
                        }

                        var filteredVerbs = verbFilter != null ? httpVerbs.Execute<IEnumerable<string>>(verbFilter) : httpVerbs;
                        foreach (var httpVerb in filteredVerbs)
                        {
                            if (operationName.Length + httpVerb.Length + 1 > 80)
                                operationName = operationName.Substring(0, (79 - httpVerb.Length));
                            await _apimProvider.CreateOrUpdateOperationPolicy(rawOperationLevelPolicyXml, $"{operationName}_{httpVerb}", operationDescription, httpVerb, proxyPath);
                        }

                        rawOperationLevelPolicyXml = RawPolicyXml();
                        inboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("inbound")!;
                        outboundAzureApimOperationPolicySection = rawOperationLevelPolicyXml.Element("policies")!.Element("outbound")!;
                    }
                }
                else
                {
                    foreach (var verb in httpVerbs)
                    {
                        await _apimProvider.CreateOrUpdateOperation(apiNameToUse, description, verb);
                    }
                }

                #endregion
            }
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