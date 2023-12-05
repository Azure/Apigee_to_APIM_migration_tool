using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ApiManagement;
using Azure.ResourceManager.ApiManagement.Models;
using Azure.ResourceManager.Resources;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Linq.Dynamic;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Core;
using System.Net.Http.Json;
using System.Net;
using static Azure.Core.HttpHeader;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel.DataAnnotations;
using System.Linq.Dynamic.Core.Tokenizer;
using System.Security.Policy;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimService : IAzureApimService
    {
        private readonly IApigeeManagementApiService _apiService;
        private readonly IApimProvider _apimProvider;
        private readonly string _apimUrl;
        private readonly List<KeyValuePair<string, string>> _policyVariables;

        public AzureApimService(IApigeeManagementApiService apiService, IApimProvider apimProvider, string apimUrl)
        {
            _apiService = apiService;
            _apimProvider = apimProvider;
            _policyVariables = new List<KeyValuePair<string, string>>();
            _apimUrl = apimUrl;
        }
        public async Task ImportApi(string apimName, string apimUrl, string resourceGroupName, string bundlePath, string proxyName, string brearToken, string oauthConfigName, string backendAppId, string azureAdTenentId)
        {
            var apiProxyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", $"{proxyName}.xml"));
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
                var apiTargetXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "targets", $"{apiTargetEndpoint}.xml"));
                endpointUrl = !apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Descendants("URL").Any() ?
                    apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("Path").Value : apiTargetXml.Element("TargetEndpoint").Element("HTTPTargetConnection").Element("URL").Value;
            }

            var defaultApiProxyEndpointXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "proxies", $"{proxyEndpointElements.First().Value}.xml"));
            string ApiBasePath = defaultApiProxyEndpointXml.Root.Element("HTTPProxyConnection").Element("BasePath").Value;

            var apiResource = await _apimProvider.CreateApi(apiName, displayName, description, apimName, resourceGroupName, revision, ApiBasePath, endpointUrl, oauthConfigName);

            var rawApiLevelPolicyXml = RawPolicyXml();

            var apiResourceOperations = apiResource.GetApiOperations();
            var apiPolicies = apiResource.GetApiPolicies();

            string[] httpVerbs = { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

            Dictionary<string, string> expressionTranslator = ExpressionTranslator();

            #region Api level Policies
            foreach (var proxyEndpoint in proxyEndpointElements)
            {
                var apiProxyEndpointXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "proxies", $"{proxyEndpoint.Value}.xml"));

                //get pre-flow request policies
                foreach (var element in apiProxyEndpointXml.Root.Element("PreFlow").Element("Request").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow request policies
                foreach (var element in apiProxyEndpointXml.Root.Element("PostFlow").Element("Request").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }

                //get pre-flow response policies
                foreach (var element in apiProxyEndpointXml.Root.Element("PreFlow").Element("Response").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow response policies
                foreach (var element in apiProxyEndpointXml.Root.Element("PostFlow").Element("Response").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }
            }

            foreach (var targetEndpoint in targetEndpointElements)
            {
                var targetEndpointXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "targets", $"{targetEndpoint.Value}.xml"));

                //get pre-flow request policies
                foreach (var element in targetEndpointXml.Root.Element("PreFlow").Element("Request").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow request policies
                foreach (var element in targetEndpointXml.Root.Element("PostFlow").Element("Request").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }

                //get pre-flow response policies
                foreach (var element in targetEndpointXml.Root.Element("PreFlow").Element("Response").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow response policies
                foreach (var element in targetEndpointXml.Root.Element("PostFlow").Element("Response").Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }
            }

            var policyParameters = new PolicyContractData
            {
                Value = WebUtility.HtmlDecode(rawApiLevelPolicyXml.ToString()),
                Format = PolicyContentFormat.RawXml
            };

            await apiPolicies.CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"policy", policyParameters);
            #endregion

            #region API Operations and policies
            // create api operations
            var rawOperationLevelPolicyXml = RawPolicyXml();

            foreach (var proxyEndpoint in proxyEndpointElements)
            {
                var apiProxyEndpointXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "proxies", $"{proxyEndpoint.Value}.xml"));

                var flows = apiProxyEndpointXml.Root.Element("Flows");

                if (flows.Elements("Flow").Count() > 0)
                {
                    foreach (var flow in flows.Elements("Flow"))
                    {
                        //get flow request policies
                        foreach (var element in flow.Element("Request").Elements("Step"))
                        {
                            string policyName = element.Element("Name").Value;
                            string operationCondition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                            var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                            var rootElement = policyXml.Root;
                            XElement newPolicy;

                            await TransformPolicy(rootElement, rootElement.Name.ToString(), rawOperationLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, operationCondition, policyName, brearToken);
                        }

                        //get flow response policies
                        foreach (var element in apiProxyEndpointXml.Root.Element("PreFlow").Element("Response").Elements("Step"))
                        {
                            string policyName = element.Element("Name").Value;
                            string operationCondition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                            var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                            var rootElement = policyXml.Root;
                            XElement newPolicy;

                            await TransformPolicy(rootElement, rootElement.Name.ToString(), rawOperationLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, operationCondition, policyName, brearToken);
                        }

                        string OperationName = flow.Attribute("name").Value;
                        string OperationDescription = flow.Element("Description").Value;
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
                            string apimOperationName = $"{OperationName.Replace(" ", "_").Trim()}_{httpVerb}";
                            var apimOperationResource = await apiResourceOperations.CreateOrUpdateAsync(WaitUntil.Completed, apimOperationName, new ApiOperationData
                            {
                                DisplayName = OperationName,
                                Description = OperationDescription,
                                Method = httpVerb,
                                UriTemplate = string.IsNullOrEmpty(proxyPath) ? "/" : proxyPath
                            });

                            var operationPolicyParameters = new PolicyContractData
                            {
                                Value = rawOperationLevelPolicyXml.ToString(),
                                Format = PolicyContentFormat.RawXml
                            };

                            await apimOperationResource.Value.GetApiOperationPolicies().CreateOrUpdateAsync(WaitUntil.Completed, $"policy", operationPolicyParameters);
                        }
                    }
                }
                else
                {
                    foreach (var verb in httpVerbs)
                    {
                        string apiOperationName = $"{apiName}_{verb}";
                        await apiResourceOperations.CreateOrUpdateAsync(WaitUntil.Completed, apiOperationName, new ApiOperationData
                        {
                            DisplayName = apiOperationName,
                            Description = description,
                            Method = verb,
                            UriTemplate = "/"
                        });
                    }
                }
            }
            #endregion

        }


        public async Task AddApiToProduct(ApiManagementProductResource apiProductResource, string apiId)
        {
            await apiProductResource.CreateOrUpdateProductApiAsync(apiId);
        }

        #region Private Methods
        private async Task ImportSharedFlow(string sharedFlowBundlePath, string sharedflowName, string resourceGroupName, string apimName, string brearToken)
        {
            var rawPolicyFragment = RawPolicyFragmentXml();

            var sharedFlowBundleXml = XDocument.Load(Path.Combine(sharedFlowBundlePath, "sharedflowbundle", $"{sharedflowName}.xml"));
            var sharedFlowElement = sharedFlowBundleXml.Element("SharedFlowBundle");
            string sharedFlowName = sharedFlowElement.Attribute("name").Value;
            string displayName = sharedFlowElement.Element("DisplayName").Value;
            string description = sharedFlowElement.Element("Description").Value;

            var sharedFlows = sharedFlowElement.Element("SharedFlows").Elements("SharedFlow");

            foreach (var sharedFlow in sharedFlows)
            {
                var sharedFlowXml = XDocument.Load(Path.Combine(sharedFlowBundlePath, "sharedflowbundle", "sharedflows", $"{sharedFlow.Value}.xml"));
                var sharedFlowRootElement = sharedFlowXml.Element("SharedFlow");
                var steps = sharedFlowRootElement.Elements("Step");
                foreach (var step in steps)
                {
                    var policyName = step.Element("Name").Value;
                    var condition = step.Element("Condition") != null ? step.Element("Condition").Value : "";
                    var policyXml = XDocument.Load(Path.Combine(sharedFlowBundlePath, "sharedflowbundle", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;
                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawPolicyFragment.Root, apimName, resourceGroupName, condition, policyName, brearToken);
                }
                await _apimProvider.CreatePolicyFragment(sharedFlowName, apimName, resourceGroupName, WebUtility.HtmlDecode(rawPolicyFragment.ToString()), description);
            }
        }
        private async Task TransformPolicy(XElement? element, string apigeePolicyName, XElement apimPolicyElement, string apimName, string apimResourceGroupName, string condition, string apigeePolicyDisplayName, string brearToken)
        {
            switch (apigeePolicyName)
            {
                case "AssignMessage":
                    bool removePolicy = element.Elements().Any(x => x.Name.ToString() == "Remove");
                    bool setPolicy = element.Elements().Any(x => x.Name.ToString() == "Set");

                    string parentElementName;
                    if (setPolicy)
                        parentElementName = "Set";
                    else
                        parentElementName = removePolicy ? "Remove" : "Add";

                    if (element.Element(parentElementName).Element("Headers") != null)
                    {
                        foreach (var header in element.Element(parentElementName).Element("Headers").Elements("Header"))
                        {
                            apimPolicyElement.Add(SetHeader(header, removePolicy, condition));
                        }
                    }


                    if (element.Element(parentElementName).Element("Payload") != null)
                    {
                        apimPolicyElement.Add(SetBody(element.Element(parentElementName).Element("Payload"), condition));
                    }

                    break;
                case "LookupCache":
                    apimPolicyElement.Add(CacheLookupValue(element, apigeePolicyDisplayName, condition));
                    break;
                case "KeyValueMapOperations":
                    apimPolicyElement.Add(SetVariable(element, apigeePolicyDisplayName, condition));
                    break;
                case "VerifyJWT":
                    apimPolicyElement.Add(ValidateJwt(element, condition));
                    break;
                case "ServiceCallout":
                    apimPolicyElement.Add(SendRequest(element, _apimUrl, condition));
                    break;
                case "ExtractVariables":
                    apimPolicyElement.Add(ExtractJsonValue(element, apigeePolicyDisplayName, condition));
                    break;
                case "OAuthV2":
                    apimPolicyElement.Add(OAuthV2(element, apigeePolicyDisplayName, condition));
                    break;
                case "PopulateCache":
                    apimPolicyElement.Add(CacheStoreValue(element, apigeePolicyDisplayName, condition));
                    break;
                case "FlowCallout":
                    string sharedFlowName = element.Element("SharedFlowBundle").Value;
                    string sharedFlowBundlePath = await DownloadSharedFlow(sharedFlowName, brearToken);
                    await ImportSharedFlow(sharedFlowBundlePath, sharedFlowName, apimResourceGroupName, apimName, brearToken);
                    apimPolicyElement.Add(IncludeFragment(sharedFlowName, condition));
                    break;
                    //default:
                    //    break;
            }
        }
        private async Task<string> DownloadSharedFlow(string sharedFlowName, string authenticationToken)
        {
            var sharedFlowMetadata = await _apiService.GetSharedFlowByName(sharedFlowName, authenticationToken);
            return await _apiService.DownloadSharedFlowBundle(sharedFlowName, sharedFlowMetadata.revision.Select(x => int.Parse(x)).Max(), authenticationToken);
        }
        private XElement SetHeader(XElement header, bool remove, string condition = null)
        {
            var name = header.Attribute("name")?.Value;
            var value = header.Value;
            if (value.StartsWith('{'))
            {
                value = value.Replace("{", "").Replace("}", "");
                foreach (var expression in ExpressionTranslator())
                {
                    value = value.Replace(expression.Key, expression.Value);
                }
                value = WebUtility.HtmlDecode($"@({value})");
            }
            var newPolicy = new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", remove ? "delete" : "override"));
            if (!remove)
                newPolicy.Add(new XElement("value", value));
            return ApplyConditionToPolicy(condition, newPolicy);
        }

        private XElement SetBody(XElement body, string condition = null)
        {
            var contentType = body.Attribute("contentType")?.Value;
            var value = body.Value;
            var newPolicy = new XElement("set-body");
            if (!contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            {
                if (value.StartsWith('{'))
                {
                    value = value.Replace("{", "").Replace("}", "");
                    foreach (var expression in ExpressionTranslator())
                    {
                        value = value.Replace(expression.Key, expression.Value);
                    }
                    value = WebUtility.HtmlDecode($"@({value})");
                }
                newPolicy.Add(new XAttribute("template", "liquid"));
            }

            newPolicy.Value = value;
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement CacheLookupValue(XElement element, string policyName, string condition = null)
        {
            var cacheKey = element.Element("CacheKey");
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;
            string variableName = element.Element("AssignTo").Value;
            string apimCacheKey = $"{keyFragment}__{keyFragmentRef}";

            var newPolicy = new XElement("cache-lookup-value", new XAttribute("key", apimCacheKey), new XAttribute("variable-name", variableName), new XAttribute("caching-type", "internal"));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement CacheStoreValue(XElement element, string policyName, string condition = null)
        {
            var cacheKey = element.Element("CacheKey");
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;
            string expiry = element.Element("ExpirySettings").Element("TimeoutInSec").Value;
            string variableName = element.Element("Source").Value;
            string apimCacheKey = $"{keyFragment}__{keyFragmentRef}";

            var newPolicy = new XElement("cache-store-value", new XAttribute("key", apimCacheKey), new XAttribute("value", $"@((string)context.Variables[\"{variableName}\"])"), new XAttribute("caching-type", "internal"), new XAttribute("duration", expiry));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement SetVariable(XElement element, string policyName, string condition = null)
        {
            var mapIdentifier = element.Attribute("mapIdentifier").Value;
            var variableName = element.Element("Get").Attribute("assignTo").Value;
            var index = element.Element("Get").Attribute("index").Value;
            var key = element.Element("Get").Element("Key").Element("Parameter").Value;

            string namedValueName = $"{mapIdentifier}-{key}-{index}";
            namedValueName = namedValueName.Replace("_", "-");

            var newPolicy = new XElement("set-variable", new XAttribute("name", variableName), new XAttribute("value", "{{" + namedValueName + "}}"));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement OAuthV2(XElement element, string policyName, string condition = null)
        {
            var asyncAttr = element.Attribute("async")?.Value;
            var continueOnError = element.Attribute("continueOnError")?.Value;
            var enabled = element.Attribute("enabled")?.Value;
            var name = element.Attribute("name")?.Value;

            var displayName = element.Element("DisplayName")?.Value;
            var operation = element.Element("Operation")?.Value;

            var newPolicy = new XElement("validate-jwt",
                new XAttribute("header-name", "Authorization"),
                new XAttribute("failed-validation-httpcode", "401"),
                new XAttribute("failed-validation-error-message", "Unauthorized. Access token is missing or invalid.")
            // additional attributes can go here, like openid-config, issuers, required-claims when needed.
            );

            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement ExtractJsonValue(XElement element, string policyName, string condition = null)
        {
            var jsonVariableName = element.Element("Source").Value;
            var variablePrefix = element.Element("VariablePrefix").Value;
            var variableName = element.Element("JSONPayload").Element("Variable").Attribute("name").Value;
            var jsonPath = element.Element("JSONPayload").Element("Variable").Element("JSONPath").Value.Replace("$", "");
            //                ((Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(context.Variables.GetValueOrDefault<string>(\" + jsonVariableName + "\"))){jsonPath})
            string apimExpression = "@{" +
                    "JObject json = JObject.Parse(context.Variables.GetValueOrDefault<string>(\"" + jsonVariableName + "\"));" +
                    "var typeCode = json.GetValue(\"" + jsonPath.Replace(".", "").Trim() + "\");" +
                    "return typeCode;" +
                    "}";

            var newPolicy = new XElement("set-variable", new XAttribute("name", $"{variablePrefix}.{variableName}"), new XAttribute("value", apimExpression));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement ValidateJwt(XElement element, string condition = null)
        {
            var token = element.Element("Source").Value;
            var key = element.Element("PublicKey").Element("Value").Attribute("ref").Value;

            var decryptionKeyelement = new XElement("decryption-keys");
            decryptionKeyelement.Add(new XElement("key", $"@(context.Variables.GetValueOrDefault<string>(\"{key}\"))"));
            string outputVariableName = $"{token}_validated";
            var newPolicy = new XElement("validate-jwt", new XAttribute("token-value", $"@(context.Variables.GetValueOrDefault<string>(\"{token}\"))"), decryptionKeyelement,
                new XAttribute("output-token-variable-name", outputVariableName));
            _policyVariables.Add(new KeyValuePair<string, string>(element.Attribute("name").Value, outputVariableName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement IncludeFragment(string fragmentName, string condition = null)
        {
            var newPolicy = new XElement("include-fragment", new XAttribute("fragment-id", fragmentName));
            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement SendRequest(XElement element, string apimUrl, string condition = null)
        {
            string requestVariable = element.Element("Request").Attribute("variable").Value;
            string responseVariable = element.Element("Response").Value;
            string continueOnError = element.Attribute("continueOnError").Value;

            var newPolicy = new XElement("send-request", new XAttribute("mode", $"new"), new XAttribute("response-variable-name", responseVariable), new XAttribute("timeout", "60"), new XAttribute("ignore-error", "true"));

            string url = element.Element("LocalTargetConnection") != null ? element.Element("LocalTargetConnection").Element("Path").Value : element.Element("HTTPTargetConnection").Element("URL").Value;
            if (url.StartsWith('/'))
                url = apimUrl + url;
            newPolicy.Add(new XElement("set-url", url));

            string verb = element.Element("Request").Element("Set").Element("Verb").Value;
            newPolicy.Add(new XElement("set-method", verb));

            var headers = element.Element("Request").Element("Set").Element("Headers").Elements("Header");
            foreach (var header in headers)
                newPolicy.Add(SetHeader(header, false));

            string PayloadContentType = element.Element("Request").Element("Set").Element("Payload").Attribute("contentType").Value;
            string PayloadContent = element.Element("Request").Element("Set").Element("Payload").Value;
            string variablePattern = @"{(.*?)}";
            foreach (Match match in Regex.Matches(PayloadContent, variablePattern))
            {
                if (match.Success && match.Groups.Count > 0)
                {
                    PayloadContent = PayloadContent.Replace("{" + match.Groups[1].Value + "}", $"@(context.Variables.GetValueOrDefault(\"{match.Groups[1].Value}\"))");
                }
            }

            var setBody = new XElement("set-body", PayloadContent);
            setBody.Add(new XAttribute("template", "liquid"));
            newPolicy.Add(setBody);

            return ApplyConditionToPolicy(condition, newPolicy);
        }
        private XElement ApplyConditionToPolicy(string condition, XElement policy)
        {
            if (!string.IsNullOrEmpty(condition))
            {
                foreach (var expression in ExpressionTranslator())
                {
                    condition = condition.Replace(expression.Key, expression.Value);
                }

                string cacheLookupPattern = @"lookupcache.(.*?).cachehit";
                foreach (Match match in Regex.Matches(condition, cacheLookupPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var policyName = match.Groups[1].Value;
                        string completeExpression = $"lookupcache.{policyName}.cachehit";
                        string variableName = _policyVariables.FirstOrDefault(x => x.Key == policyName).Value;
                        condition = condition.Replace(completeExpression, $"context.Variables.ContainsKey(\"{variableName}\")");
                    }
                }

                string jwtValidPattern = @"jwt.(.*?).valid";
                foreach (Match match in Regex.Matches(condition, jwtValidPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var policyName = match.Groups[1].Value;
                        string completeExpression = $"jwt.{policyName}.valid";
                        string variableName = _policyVariables.FirstOrDefault(x => x.Key == policyName).Value;
                        condition = condition.Replace(completeExpression, $"context.Variables.ContainsKey(\"{variableName}\")");
                    }
                }


                string variableEqualsNullPattern = @"[^ ]* == null";
                foreach (Match match in Regex.Matches(condition, variableEqualsNullPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            variableName = variableName.Replace("(", "").Replace(")", "");
                            condition = condition.Replace(variableName, $"!context.Variables.ContainsKey(\"{variableName.Replace("== null", "").Trim()}\")");
                        }
                    }
                }

                string variableDoesNotEqualsNullPattern = @"[^ ]* != null";
                foreach (Match match in Regex.Matches(condition, variableDoesNotEqualsNullPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            variableName = variableName.Replace("(", "").Replace(")", "");
                            condition = condition.Replace(variableName, $"context.Variables.ContainsKey(\"{variableName.Replace("!= null", "").Trim()}\")");
                        }
                    }
                }

                //foreach (var variable in _policyVariables)
                //{
                //    condition = condition.Replace($"{variable.Value} == null", $"!context.Variables.ContainsKey(\"{variable.Value}\")");
                //    condition = condition.Replace($"{variable.Value} != null", $"context.Variables.ContainsKey(\"{variable.Value}\")");
                //}

                condition = WebUtility.HtmlDecode($"@({condition})");
                var conditionelement = new XElement("choose", new XElement("when", ""));
                conditionelement.Element("when").SetAttributeValue("condition", condition);
                conditionelement.Element("when").Add(policy);
                return conditionelement;
            }
            else
                return policy;
        }
        private Dictionary<string, string> ExpressionTranslator()
        {
            var expressionList = new Dictionary<string, string>();
            expressionList.Add("request.verb", "context.Operation.Method");
            expressionList.Add("request.header.origin", "context.Request.Headers.GetValueOrDefault(\"origin\")");
            expressionList.Add("request.header.Access-Control-Request-Method", "context.Request.Headers.GetValueOrDefault(\"Access-Control-Request-Method\")");
            expressionList.Add(" AND ", " && ");
            expressionList.Add(" and ", " && ");
            expressionList.Add(" or ", " || ");
            expressionList.Add(" OR ", " || ");
            expressionList.Add(" = ", " == ");

            return expressionList;
        }
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
        private XDocument RawPolicyFragmentXml()
        {
            string rawFragment = @"<fragment></fragment>";

            return XDocument.Parse(rawFragment);
        }
        #endregion
    }
}

