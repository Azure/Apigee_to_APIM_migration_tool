using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using Azure;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimService : IAzureApimService
    {
        private readonly IApigeeManagementApiService _apiService;
        private readonly IApimProvider _apimProvider;
        private readonly string _apimUrl;
        private readonly List<KeyValuePair<string, string>> _policyVariables;

        private string _apigeeAuthToken;
        private string _apigeeEnvironmentName;
        private string _azureKeyVaultName;
        private string _apigeeProxyName;

        public AzureApimService(IApigeeManagementApiService apiService, IApimProvider apimProvider, string apimUrl)
        {
            _apiService = apiService;
            _apimProvider = apimProvider;
            _policyVariables = new List<KeyValuePair<string, string>>();
            _apimUrl = apimUrl;
        }
        public async Task ImportApi(string apimName, string bundlePath, string proxyName, string bearerToken, string oauthConfigName, string environment, string keyVaultName)
        {
            _apigeeAuthToken = bearerToken;
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

            var apiResource = await _apimProvider.CreateApi(apiName, displayName, description, apimName, revision, ApiBasePath, endpointUrl, oauthConfigName);

            var rawApiLevelPolicyXml = RawPolicyXml();

            //var apiResourceOperations = apiResource.GetApiOperations();
            //var apiPolicies = apiResource.GetApiPolicies();

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

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, condition, policyName, bearerToken);

                }

                //get post-flow request policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, condition, policyName, bearerToken);
                }

                //get pre-flow response policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, condition, policyName, bearerToken);

                }

                //get post-flow response policies
                foreach (var element in apiProxyEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, condition, policyName, bearerToken);
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

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, condition, policyName, bearerToken);

                }

                //get post-flow request policies
                foreach (var element in targetEndpointXml.Root?.Element("PostFlow")?.Element("Request")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("inbound"), apimName, condition, policyName, bearerToken);
                }

                //get pre-flow response policies
                foreach (var element in targetEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, condition, policyName, bearerToken);

                }

                //get post-flow response policies
                foreach (var element in targetEndpointXml.Root?.Element("PostFlow")?.Element("Response")?.Elements("Step"))
                {
                    string policyName = element.Element("Name").Value;
                    string condition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                    var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                    var rootElement = policyXml.Root;
                    XElement newPolicy;

                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawApiLevelPolicyXml.Element("policies").Element("outbound"), apimName, condition, policyName, bearerToken);
                }
            }

            await _apimProvider.CreatePolicy(rawApiLevelPolicyXml);
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

                            await TransformPolicy(rootElement, rootElement.Name.ToString(), rawOperationLevelPolicyXml.Element("policies").Element("inbound"), apimName, operationCondition, policyName, bearerToken);
                        }

                        //get flow response policies
                        foreach (var element in apiProxyEndpointXml.Root?.Element("PreFlow")?.Element("Response")?.Elements("Step"))
                        {
                            string policyName = element.Element("Name").Value;
                            string operationCondition = element.Element("Condition") != null ? element.Element("Condition").Value : "";

                            var policyXml = XDocument.Load(Path.Combine(bundlePath, "apiproxy", "policies", $"{policyName}.xml"));
                            var rootElement = policyXml.Root;
                            XElement newPolicy;

                            await TransformPolicy(rootElement, rootElement.Name.ToString(), rawOperationLevelPolicyXml.Element("policies").Element("outbound"), apimName, operationCondition, policyName, bearerToken);
                        }

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
        private async Task ImportSharedFlow(string sharedFlowBundlePath, string sharedflowName, string apimName, string brearToken)
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
                    await TransformPolicy(rootElement, rootElement.Name.ToString(), rawPolicyFragment.Root, apimName, condition, policyName, brearToken);
                }
                await _apimProvider.CreatePolicyFragment(sharedFlowName, apimName, WebUtility.HtmlDecode(rawPolicyFragment.ToString()), description);
            }
        }
        private async Task TransformPolicy(XElement? element, string apigeePolicyName, XElement apimPolicyElement, string apimName, string condition, string apigeePolicyDisplayName, string brearToken)
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
                    foreach (var setVariableElement in await SetVariable(element, _apigeeProxyName, apimName, apigeePolicyDisplayName, condition))
                        apimPolicyElement.Add(setVariableElement);
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
                    await ImportSharedFlow(sharedFlowBundlePath, sharedFlowName, apimName, brearToken);
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
        private async Task<IEnumerable<XElement>> SetVariable(XElement element, string proxyName, string apimName, string policyName, string condition = null)
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
                        await _apimProvider.AddNamedValue(apimName, proxyName, mapIdentifier, key, apigeeKeyValueMap.Encrypted, keyValueMapEntry.Value, _azureKeyVaultName);
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