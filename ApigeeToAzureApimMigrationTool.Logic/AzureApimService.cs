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
        private readonly ArmClient _client;
        private readonly IApigeeManagementApiService _apiService;
        private readonly string _authToken;
        private readonly List<KeyValuePair<string, string>> _policyVariables;
        private readonly string _apiUrl;
        private readonly HttpClient _httpClient;
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _subscriptionId;

        private string _apigeeAuthToken;
        private string _apigeeEnvironmentName;
        private string _azureKeyVaultName;
        private string _apigeeProxyName;

        public AzureApimService(string subscriptionId, string tenantId, string clientId, string clientSecret, IApigeeManagementApiService apiService, string apimUrl)
        {
            _client = new ArmClient(new Azure.Identity.ClientSecretCredential(tenantId, clientId, clientSecret));
            _subscriptionId = subscriptionId;
            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _apiService = apiService;
            _apiUrl = apimUrl;
            _policyVariables = new List<KeyValuePair<string, string>>();
            _httpClient = new HttpClient();
        }
        public async Task ImportApi(string apimName, string apimUrl, string resourceGroupName, string bundlePath, string proxyName, string brearToken,
            string oauthConfigName, string backendAppId, string azureAdTenentId, string environment, string keyVaultName)
        {
            _apigeeAuthToken = brearToken;
            _apigeeEnvironmentName = environment;
            _azureKeyVaultName = keyVaultName;
            _apigeeProxyName = proxyName;

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

            var apiResource = await CreateApi(apiName, apimUrl, displayName, description, apimName, resourceGroupName, revision, ApiBasePath, endpointUrl, oauthConfigName);

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
                foreach (var element in apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow request policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }

                //get pre-flow response policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow response policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step"))
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
                foreach (var element in targetEndpointXml.Root?.Element("PreFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow request policies
                foreach (var element in targetEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, resourceGroupName, condition, policyName, brearToken);
                }

                //get pre-flow response policies
                foreach (var element in targetEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, resourceGroupName, condition, policyName, brearToken);

                }

                //get post-flow response policies
                foreach (var element in targetEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step"))
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
                        foreach (var element in apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
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
                        string apiOperationName = $"API_Operation_{verb}";
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

        public async Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string resourceGroupName, string apimName)
        {
            var subscriptions = _client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(resourceGroupName);
            ApiManagementServiceResource apimResource = await resourceGroup.GetApiManagementServiceAsync(apimName);
            ApiManagementProductCollection apiProducts = apimResource.GetApiManagementProducts();
            var apiProduct = await apiProducts.CreateOrUpdateAsync(WaitUntil.Completed, name.Trim().Replace(" ", "_"), new ApiManagementProductData
            {
                Description = description,
                DisplayName = displayName,
                State = ApiManagementProductState.Published
            });

            ApiManagementProductResource apiProductResource = _client.GetApiManagementProductResource(apiProduct.Value.Id);

            return apiProductResource;
        }

        public async Task AddApiToProduct(ApiManagementProductResource apiProductResource, string apiId)
        {
            await apiProductResource.CreateOrUpdateProductApiAsync(apiId);
        }

        #region Private Methods
        private async Task CreatePolicyFragment(string policyFragmentName, string apimName, string apimResourceGroupName, string policyFragmentXml, string policyFragmentDescription)
        {
            var body = new
            {
                properties = new
                {
                    value = policyFragmentXml,
                    description = policyFragmentDescription,
                    format = "rawxml"
                }
            };
            _httpClient.DefaultRequestHeaders.Clear();
            var token = await GetAccessToken();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            var response = await _httpClient.PutAsJsonAsync($"https://management.azure.com/subscriptions/{_subscriptionId}/resourceGroups/{apimResourceGroupName}/providers/Microsoft.ApiManagement/service/{apimName}/policyFragments/{policyFragmentName}?api-version=2023-03-01-preview", body);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"can't create Policy Fragment. Status code: {response.StatusCode} - {response.Content.ToString()}");

            Thread.Sleep(5000);
        }
        private async Task<string> GetAccessToken()
        {
            var credentials = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
            var result = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
            return result.Token;
        }
        private async Task<ApiResource> CreateApi(string apiName, string apimUrl, string apiDisplayName, string apiDescription, string apimName, string resourceGroupName,
                string revision, string apiPath, string backendUrl, string oauthConfigurationName = null)
        {
            try
            {
                var subscriptions = _client.GetSubscriptions();
                SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
                ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
                ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(resourceGroupName);
                ApiManagementServiceResource apimResource = await resourceGroup.GetApiManagementServiceAsync(apimName);
                ApiCollection apiCollection = apimResource.GetApis();
                if (!backendUrl.Contains("http"))
                {
                    backendUrl = apimUrl + backendUrl;
                }
                var api = new ApiCreateOrUpdateContent
                {
                    ApiRevision = revision,
                    ApiType = ApiType.Http,
                    DisplayName = apiDisplayName,
                    Description = apiDescription,
                    Path = apiPath,
                    ServiceUri = new Uri(backendUrl),
                    IsSubscriptionRequired = false,
                    IsCurrent = true,
                    Protocols =
                    {
                        ApiOperationInvokableProtocol.Https
                    }
                };

                if (!string.IsNullOrEmpty(oauthConfigurationName))
                {
                    AuthenticationSettingsContract authenticationSetting = new AuthenticationSettingsContract();
                    authenticationSetting.OAuth2 = new OAuth2AuthenticationSettingsContract()
                    {
                        AuthorizationServerId = oauthConfigurationName
                    };
                    api.AuthenticationSettings = authenticationSetting;
                }

                var importedApi = await apiCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, apiName.Trim().Replace(" ", ""), api);

                ApiResource apiResource = _client.GetApiResource(importedApi.Value.Id);

                return apiResource;

            }
            catch (Exception)
            {
                throw;
            }
        }
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
                await CreatePolicyFragment(sharedFlowName, apimName, resourceGroupName, WebUtility.HtmlDecode(rawPolicyFragment.ToString()), description);
            }
        }
        private async Task TransformPolicy(XElement? element, string apigeePolicyName, XElement apimPolicyElement, string apimName, string apimResourceGroupName, string condition, string apigeePolicyDisplayName, string brearToken)
        {
            switch (apigeePolicyName)
            {
                case "AssignMessage":

                    if (element.Element("Add")?.Element("Headers") != null)
                    {
                        foreach (var header in element.Element("Add").Element("Headers").Elements("Header"))
                        {
                            apimPolicyElement.Add(SetHeader(header, false, condition));
                        }
                    }

                    if (element.Element("Set")?.Element("Headers") != null)
                    {
                        foreach (var header in element.Element("Set").Element("Headers").Elements("Header"))
                        {
                            apimPolicyElement.Add(SetHeader(header, false, condition));
                        }
                    }


                    if (element.Element("Remove")?.Element("Headers") != null)
                    {
                        foreach (var header in element.Element("Remove").Element("Headers").Elements("Header"))
                        {
                            apimPolicyElement.Add(SetHeader(header, true, condition));
                        }
                    }

                    if (element.Element("Add")?.Element("Payload") != null)
                    {
                        apimPolicyElement.Add(SetBody(element.Element("Add").Element("Payload"), condition));
                    }

                    if (element.Element("Set")?.Element("Payload") != null)
                    {
                        apimPolicyElement.Add(SetBody(element.Element("Set").Element("Payload"), condition));
                    }

                    break;
                case "LookupCache":
                    apimPolicyElement.Add(CacheLookupValue(element, apigeePolicyDisplayName, condition));
                    break;
                case "KeyValueMapOperations":
                    foreach (var setVariableElement in await SetVariable(element, _apigeeProxyName, apimName, apimResourceGroupName, apigeePolicyDisplayName, condition))
                        apimPolicyElement.Add(setVariableElement);
                    break;
                case "VerifyJWT":
                    apimPolicyElement.Add(ValidateJwt(element, condition));
                    break;
                case "ServiceCallout":
                    apimPolicyElement.Add(await SendRequest(element, _apiUrl, condition, _apigeeEnvironmentName));
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
        private async Task<IEnumerable<XElement>> SetVariable(XElement element, string proxyName, string apimName, string resourceGroupName, string policyName, string condition = null)
        {
            XDocument setVariablePolicies = new XDocument();
            setVariablePolicies.Add(new XElement("Root"));
            var mapIdentifier = element.Attribute("mapIdentifier").Value;

            var getElements = element.Elements("Get");
            if (getElements != null)
                foreach (var getElement in getElements)
                {
                    var index = getElement.Attribute("index").Value;
                    var key = getElement.Element("Key").Element("Parameter").Value;
                    var variableName = getElement.Attribute("assignTo").Value;


                    var apigeeKeyValueMap = await _apiService.GetKeyValueMapByName(proxyName, _apigeeEnvironmentName, mapIdentifier, _apigeeAuthToken);
                    if (apigeeKeyValueMap != null)
                    {
                        var keyValueMapEntry = apigeeKeyValueMap.Entry.FirstOrDefault(x => x.Name.Equals(key));
                        if (keyValueMapEntry == null)
                            throw new Exception($"Can't find entry {key} under mapIdentifier {mapIdentifier} in Apigee");
                        await AddNamedValue(resourceGroupName, apimName, proxyName, mapIdentifier, key, apigeeKeyValueMap.Encrypted, keyValueMapEntry.Value, _azureKeyVaultName);
                    }

                    string namedValueName = $"{mapIdentifier}-{key}";
                    namedValueName = namedValueName.Replace("_", "-");

                    var policy = new XElement("set-variable", new XAttribute("name", variableName), new XAttribute("value", "{{" + namedValueName + "}}"));
                    _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
                    policy = ApplyConditionToPolicy(condition, policy);
                    setVariablePolicies.Root.Add(policy);
                }
            return setVariablePolicies.Root.Elements();

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
        private async Task<XElement> SendRequest(XElement element, string apimUrl, string condition = null, string environment = null)
        {
            string requestVariable = element.Element("Request").Attribute("variable").Value;
            string responseVariable = element.Element("Response").Value;
            string continueOnError = element.Attribute("continueOnError").Value;

            var newPolicy = new XElement("send-request", new XAttribute("mode", $"new"), new XAttribute("response-variable-name", responseVariable), new XAttribute("timeout", "60"), new XAttribute("ignore-error", $"{continueOnError}"));

            string url = string.Empty;
            string targetServerName = string.Empty;

            if (element.Element("LocalTargetConnection") != null && element.Element("LocalTargetConnection").Element("Path") != null)
                url = element.Element("LocalTargetConnection").Element("Path").Value;
            else if (element.Element("HTTPTargetConnection") != null && element.Element("HTTPTargetConnection").Element("URL") != null)
                url = element.Element("HTTPTargetConnection").Element("URL").Value;
            else if (element.Element("HTTPTargetConnection") != null && element.Element("HTTPTargetConnection").Element("LoadBalancer") != null
                && element.Element("HTTPTargetConnection").Element("LoadBalancer").Elements("Server").Any())
            {

                targetServerName = element.Element("HTTPTargetConnection").Element("LoadBalancer").Elements("Server").First().Attribute("name").Value;
                if (string.IsNullOrEmpty(environment))
                    throw new Exception($"service callout policy is using a load balancer as target connection. Environment input parameter must be provided in order to migrate target server {targetServerName}");
                var targetServerResponse = await _apiService.GetTargetServerByName(targetServerName, environment, _apigeeAuthToken);
                if (targetServerResponse == null)
                    throw new Exception($"Can't read Target Server information for {targetServerName} in the {environment} env");
                string protocol = "http://";
                string port = string.Empty;
                if (targetServerResponse.SSLInfo.Enabled)
                    protocol = "https://";
                if (!(new int[] { 80, 443 }).Contains(targetServerResponse.Port))
                    port = $":{targetServerResponse.Port}";

                url = $"{protocol}{targetServerResponse.Host}{port}";
            }

            if (url.StartsWith('/'))
                url = apimUrl + url;

            if (element.Element("Request").Element("Set").Element("Path") != null)
            {
                string path = element.Element("Request").Element("Set").Element("Path").Value;
                if (path.StartsWith("{"))
                {
                    url = url + "/" + $"@((string)context.Variables[\"{path.Replace("{", "").Replace("}", "")}\"])";
                }
            }

            newPolicy.Add(new XElement("set-url", url));

            string verb = element.Element("Request").Element("Set").Element("Verb") != null ? element.Element("Request").Element("Set").Element("Verb").Value : "GET";
            newPolicy.Add(new XElement("set-method", verb));

            var headers = element.Element("Request").Element("Set")?.Element("Headers")?.Elements("Header");
            if (headers != null)
                foreach (var header in headers)
                    newPolicy.Add(SetHeader(header, false));

            string PayloadContentType = element.Element("Request").Element("Set")?.Element("Payload")?.Attribute("contentType").Value;
            string PayloadContent = element.Element("Request").Element("Set")?.Element("Payload")?.Value;
            string variablePattern = @"{(.*?)}";
            if (!string.IsNullOrEmpty(PayloadContent))
            {
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
            }

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

                string variableEqualsValue = @"[^ ]* (=|==|!=) [^ ]*";
                foreach (Match match in Regex.Matches(condition, variableEqualsValue))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        var conditionOperator = match.Groups[1].Value;
                        var variableAndValue = variableName.Replace("(", "").Replace(")", "").Split(conditionOperator.Trim());
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            var variableValue = variableAndValue[1].Trim();
                            var apimVariableName = variableAndValue[0].Trim();
                            conditionOperator = conditionOperator.Trim().StartsWith("=") ? "==" : "!=";
                            condition = condition.Replace(variableName, $"context.Variables.GetValueOrDefault<{GetDataTypeFromStringValue(variableValue)}>(\"{apimVariableName}\") {conditionOperator} {variableValue}");
                        }
                    }
                }



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

        private async Task AddNamedValue(string resourceGroupName, string apimName, string proxyName, string mapIdentifier, string keyName, bool isSecret, string value, string keyVaultName)
        {
            var subscriptions = _client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(resourceGroupName);
            ApiManagementServiceResource apimResource = await resourceGroup.GetApiManagementServiceAsync(apimName);
            ApiManagementNamedValueCollection namedValues = apimResource.GetApiManagementNamedValues();
            string namedValueName = $"{mapIdentifier}-{keyName}";
            var namedValueContent = new ApiManagementNamedValueCreateOrUpdateContent
            {
                DisplayName = namedValueName,
                Tags = { proxyName, mapIdentifier }
            };
            if (isSecret)
            {
                namedValueContent.IsSecret = true;
                {
                    if (!string.IsNullOrEmpty(keyVaultName))
                    {
                        namedValueContent.KeyVault = new KeyVaultContractCreateProperties { SecretIdentifier = $"https://{keyVaultName}.vault.azure.net/secrets/{namedValueName}" };
                    }
                    else
                    {
                        namedValueContent.Value = "MUST-BE-UPDATED";
                    }
                }
            }
            else
                namedValueContent.Value = value;

            await namedValues.CreateOrUpdateAsync(WaitUntil.Completed, namedValueName, namedValueContent);
        }

        private string GetDataTypeFromStringValue(string str)
        {

            bool boolValue;
            Int32 intValue;
            Int64 bigintValue;
            double doubleValue;
            DateTime dateValue;

            if (bool.TryParse(str, out boolValue))
                return "bool";
            else if (Int32.TryParse(str, out intValue))
                return "int";
            else if (Int64.TryParse(str, out bigintValue))
                return "Int64";
            else if (double.TryParse(str, out doubleValue))
                return "double";
            else if (DateTime.TryParse(str, out dateValue))
                return "DateTime";
            else return "string";

        }

        #endregion
    }
}