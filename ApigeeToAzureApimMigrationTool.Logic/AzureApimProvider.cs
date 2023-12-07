using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using Azure.ResourceManager;
using Azure.ResourceManager.ApiManagement.Models;
using Azure.ResourceManager.ApiManagement;
using Azure.ResourceManager.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Azure;
using System.Xml.Linq;
using System.Net;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimProvider : IApimProvider
    {
        private readonly ArmClient _client;
        private readonly string _apiUrl;
        private readonly HttpClient _httpClient;
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _resourceGroupName;
        private readonly string _subscriptionId;

        private ApiResource _apiResource;
        private ApiPolicyCollection _apiPolicies;
        private ApiManagementProductResource _productResource;
        private ApiOperationCollection _apiOperations;

        public AzureApimProvider(string subscriptionId, string tenantId, string clientId, string clientSecret, string resourceGroupName, string apimUrl)
        {
            _client = new ArmClient(new Azure.Identity.ClientSecretCredential(tenantId, clientId, clientSecret));
            _subscriptionId = subscriptionId;
            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _resourceGroupName = resourceGroupName;
            _apiUrl = apimUrl;
            _httpClient = new HttpClient();
        }

        public async Task<ApiResource> CreateApi(string apiName, string apiDisplayName, string apiDescription, string apimName,
            string revision, string apiPath, string backendUrl, string? oauthConfigurationName = null)
        {
            try
            {
                var subscriptions = _client.GetSubscriptions();
                SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
                ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
                ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(_resourceGroupName);
                ApiManagementServiceResource apimResource = await resourceGroup.GetApiManagementServiceAsync(apimName);
                ApiCollection apiCollection = apimResource.GetApis();
                if (!backendUrl.Contains("http"))
                {
                    backendUrl = _apiUrl + backendUrl;
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

                _apiResource = _client.GetApiResource(importedApi.Value.Id);

                return _apiResource;

            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string apimName)
        {
            var subscriptions = _client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(_resourceGroupName);
            ApiManagementServiceResource apimResource = await resourceGroup.GetApiManagementServiceAsync(apimName);
            ApiManagementProductCollection apiProducts = apimResource.GetApiManagementProducts();
            var apiProduct = await apiProducts.CreateOrUpdateAsync(WaitUntil.Completed, name.Trim().Replace(" ", "_"), new ApiManagementProductData
            {
                Description = description,
                DisplayName = displayName,
                State = ApiManagementProductState.Published
            });

            _productResource = _client.GetApiManagementProductResource(apiProduct.Value.Id);

            return _productResource;
        }

        public async Task AddApiToProduct(string apiId)
        {
            await _productResource.CreateOrUpdateProductApiAsync(apiId);
        }

        public async Task CreateOrUpdateOperation(string apiName, string description, string httpVerb)
        {
            if (_apiOperations == null)
            {
                _apiOperations = _apiResource.GetApiOperations();
            }

            string apiOperationName = $"{apiName}_{httpVerb}";
            await _apiOperations.CreateOrUpdateAsync(WaitUntil.Completed, apiOperationName, new ApiOperationData
            {
                DisplayName = apiOperationName,
                Description = description,
                Method = httpVerb,
                UriTemplate = "/"
            });

        }

        public async Task CreateOrUpdateOperationPolicy(XDocument operationPolicyXml, string operationName, string operationDescription, string httpVerb, string proxyPath)
        {
            if (_apiOperations == null)
            {
                _apiOperations = _apiResource.GetApiOperations();
            }

            string apimOperationName = $"{operationName.Replace(" ", "_").Trim()}_{httpVerb}";
            var apimOperationResource = await _apiOperations.CreateOrUpdateAsync(WaitUntil.Completed, apimOperationName, new ApiOperationData
            {
                DisplayName = operationName,
                Description = operationDescription,
                Method = httpVerb,
                UriTemplate = string.IsNullOrEmpty(proxyPath) ? "/" : proxyPath
            });

            var operationPolicyParameters = new PolicyContractData
            {
                Value = operationPolicyXml.ToString(),
                Format = PolicyContentFormat.RawXml
            };

            await apimOperationResource.Value.GetApiOperationPolicies().CreateOrUpdateAsync(WaitUntil.Completed, $"policy", operationPolicyParameters);

        }

        public async Task CreatePolicy(XDocument? policyXml)
        {
            if (policyXml == null)
            {
                throw new ArgumentNullException(nameof(policyXml));
            }

            var policyParameters = new PolicyContractData
            {
                Value = WebUtility.HtmlDecode(policyXml.ToString()),
                Format = PolicyContentFormat.RawXml
            };

            _apiPolicies = _apiResource.GetApiPolicies();
            await _apiPolicies.CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"policy", policyParameters);

        }


        public async Task CreatePolicyFragment(string policyFragmentName, string apimName, string policyFragmentXml, string policyFragmentDescription)
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
            var response = await _httpClient.PutAsJsonAsync($"https://management.azure.com/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.ApiManagement/service/{apimName}/policyFragments/{policyFragmentName}?api-version=2023-03-01-preview", body);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"can't create Policy Fragment. Status code: {response.StatusCode} - {response.Content.ToString()}");

            // WHY?
            Thread.Sleep(5000);
        }

        private async Task<string> GetAccessToken()
        {
            var credentials = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
            var result = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
            return result.Token;
        }

    }

}
