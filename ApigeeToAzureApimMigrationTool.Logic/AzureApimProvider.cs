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
using ApigeeToApimMigrationTool.Core.Config;
using System.Collections;
using System.ComponentModel;
using System.Web;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class AzureApimProvider : IApimProvider
    {
        public string ApimName { get; private set; }
        public string ApimUrl { get; private set; }

        private readonly HttpClient _httpClient;
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _resourceGroupName;
        private readonly string _subscriptionId;
        private readonly string? _keyVaultName;

        private ArmClient? _client;
        private ResourceGroupResource? _resourceGroup;

        private ApiResource? _apiResource;
        private ApiPolicyCollection? _apiPolicies;
        private ApiManagementProductResource? _productResource;
        private ApiOperationCollection? _apiOperations;

        public AzureApimProvider(ApimConfiguration apimConfiguration, EntraConfiguration entraConfiguration, string keyVaultName)
        {
            _subscriptionId = entraConfiguration.SubscriptionId;
            _tenantId = entraConfiguration.TenantId;
            _clientId = entraConfiguration.AppId;
            _clientSecret = entraConfiguration.Password;
            _resourceGroupName = apimConfiguration.ResourceGroup;
            _keyVaultName = keyVaultName;

            ApimName = apimConfiguration.Name;
            ApimUrl = apimConfiguration.Url;

            // TODO: instantiate properly... but does it matter in a console app?
            _httpClient = new HttpClient();
        }

        private async Task InitializeArmClient()
        {
            _client = new ArmClient(new Azure.Identity.ClientSecretCredential(_tenantId, _clientId, _clientSecret));

            var subscriptions = _client.GetSubscriptions();
            SubscriptionResource subscription = subscriptions.Get(_subscriptionId);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            _resourceGroup = await resourceGroups.GetAsync(_resourceGroupName);
        }

        public async Task<ApiResource> CreateApi(string apiName, string apiDisplayName, string apiDescription, string apimName,
            string revision, string apiPath, string backendUrl, string? oauthConfigurationName = null)
        {
            if (_resourceGroup == null)
            {
                await InitializeArmClient();
            }

            try
            {
                ApiManagementServiceResource apimResource = await _resourceGroup.GetApiManagementServiceAsync(apimName);
                ApiCollection apiCollection = apimResource.GetApis();
                if (!backendUrl.Contains("http"))
                {
                    backendUrl = ApimUrl + backendUrl;
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

                if (apiName.Length > 80)
                {
                    apiName = apiName.Substring(0, 80);
                    apiName = RemoveTrailingSpecialCharacters(apiName);
                }


                var importedApi = await apiCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, apiName.Trim().Replace(" ", ""), api);

                _apiResource = _client.GetApiResource(importedApi.Value.Id);

                return _apiResource;

            }
            catch (Exception e)
            {
                throw new Exception($"Unable to create API: {e.Message}", e);
            }
        }

        public async Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string apimName)
        {
            if (_resourceGroup == null)
            {
                await InitializeArmClient();
            }

            ApiManagementServiceResource apimResource = await _resourceGroup.GetApiManagementServiceAsync(apimName);
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
            if (_productResource == null)
            {
                throw new Exception($"Cannot add API {apiId} to product: Product not yet created.");
            }

            await _productResource.CreateOrUpdateProductApiAsync(apiId);
        }

        public async Task CreateOrUpdateOperation(string apiName, string description, string httpVerb)
        {
            if (_apiResource == null)
                throw new Exception($"Cannot add operation to API {apiName}: API not yet created.");

            _apiOperations = _apiResource.GetApiOperations();

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
            try
            {

                if (_apiResource == null)
                {
                    throw new Exception($"Cannot add operation policy to API {operationName}: API not yet created.");
                }

                if (_apiOperations == null)
                {
                    _apiOperations = _apiResource.GetApiOperations();
                }

                string apimOperationName = $"{operationName.Replace(" ", "_").Trim()}";
                var apimOperationResource = await _apiOperations.CreateOrUpdateAsync(WaitUntil.Completed, apimOperationName, new ApiOperationData
                {
                    DisplayName = operationName,
                    Description = operationDescription,
                    Method = httpVerb,
                    UriTemplate = string.IsNullOrEmpty(proxyPath) ? "/" : proxyPath
                });

                var operationPolicyParameters = new PolicyContractData
                {
                    Value = HttpUtility.HtmlDecode(operationPolicyXml.ToString()),
                    Format = PolicyContentFormat.RawXml
                };

                await apimOperationResource.Value.GetApiOperationPolicies().CreateOrUpdateAsync(WaitUntil.Completed, $"policy", operationPolicyParameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"erro in policy XML: {HttpUtility.HtmlDecode(operationPolicyXml.ToString())}, /n Error: {ex.ToString()}");
            }

        }

        public async Task CreatePolicy(XDocument policyXml)
        {
            if (_apiResource == null)
                throw new Exception($"Cannot create policy for API: API not yet created.");

                _apiPolicies = _apiResource.GetApiPolicies();

            var policyParameters = new PolicyContractData
            {
                Value = HttpUtility.HtmlDecode(policyXml.ToString()),
                Format = PolicyContentFormat.RawXml
            };

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
            KeyValuePair<string, IEnumerable<string>>? location = response.Headers.FirstOrDefault(x => x.Key == "Location");
            KeyValuePair<string, IEnumerable<string>>? azureAsyncOperation = response.Headers.FirstOrDefault(x => x.Key == "Azure-AsyncOperation");

            if (location != null)
            {
                var statusCheckResponse = await _httpClient.GetAsync(location.Value.Value.First());
                var statusCheckContent = await statusCheckResponse.Content.ReadAsStringAsync();
                if (!statusCheckResponse.IsSuccessStatusCode)
                    throw new Exception($"Error while creating the policy fragment. Error details: {statusCheckContent}");
            }
            else if (location == null && azureAsyncOperation != null)
            {
                while (true)
                {
                    var statusCheckResponse = await _httpClient.GetAsync(azureAsyncOperation.Value.Value.First());

                    if (statusCheckResponse.Headers.Any(x => x.Key == "Location"))
                        break;

                    if (statusCheckResponse.Headers.Any(x => x.Key == "Retry-After"))
                        Thread.Sleep(int.Parse(response.Headers.First(x => x.Key == "Retry-After").Value.First()));

                }
            }


            // WHY?
            //Thread.Sleep(5000);
        }

        public async Task AddNamedValue(string apimName, string proxyName, string mapIdentifier, string keyName, bool isSecret, string value, int index = 1)
        {
            if (_resourceGroup == null)
            {
                await InitializeArmClient();
            }

            ApiManagementServiceResource apimResource = await _resourceGroup.GetApiManagementServiceAsync(apimName);
            ApiManagementNamedValueCollection namedValues = apimResource.GetApiManagementNamedValues();

            string namedValueName = $"{mapIdentifier}-{keyName}-{index}";
            namedValueName = namedValueName.Replace("_", "-");
            if (namedValueName.Length > 80)
            {
                namedValueName = namedValueName.Substring(0, 80);
                namedValueName = RemoveTrailingSpecialCharacters(namedValueName);
            }



            var namedValueContent = new ApiManagementNamedValueCreateOrUpdateContent
            {
                DisplayName = namedValueName,
                Tags = { (proxyName.Length > 64 ? proxyName.Substring(0, 64) : proxyName), mapIdentifier.Length > 64 ? mapIdentifier.Substring(0, 64) : mapIdentifier }
            };
            if (isSecret)
            {
                namedValueContent.IsSecret = true;
                {
                    if (!string.IsNullOrEmpty(_keyVaultName))
                    {
                        namedValueContent.KeyVault = new KeyVaultContractCreateProperties { SecretIdentifier = $"https://{_keyVaultName}.vault.azure.net/secrets/{namedValueName}" };
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

        public async Task UpdateApiSubscriptionSetting(string apimName, string proxyName, string headerName = "", string queryParameterName = "")
        {
            try
            {
                if (_resourceGroup == null)
                {
                    await InitializeArmClient();
                }

                ApiManagementServiceResource apimResource = await _resourceGroup.GetApiManagementServiceAsync(apimName);

                _apiResource = apimResource.GetApi(proxyName);

                if (string.IsNullOrEmpty(headerName) && string.IsNullOrEmpty(queryParameterName))
                {
                    throw new Exception("Both header and query parameter names cannot be empty");
                }


                var apiSetting = new ApiCreateOrUpdateContent
                {
                    IsSubscriptionRequired = true,
                    SubscriptionKeyParameterNames = new SubscriptionKeyParameterNamesContract
                    {
                        Header = headerName,
                        Query = queryParameterName,

                    }
                };

                var apiCollection = apimResource.GetApis();
                await apiCollection.CreateOrUpdateAsync(WaitUntil.Completed, proxyName, apiSetting);
            }
            catch (Exception ex)
            {

                //TODO: Log the error 
            }

        }

        public string RemoveTrailingSpecialCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Define the special characters
            char[] specialChars = new char[] { '_', '-', '+', '|', '\\', '/' };

            // Loop from the end of the string until a non-special character is found
            int i = input.Length - 1;
            while (i >= 0 && specialChars.Contains(input[i]))
            {
                i--;
            }

            // Return the substring without the trailing special characters
            return input.Substring(0, i + 1);
        }

        private async Task<string> GetAccessToken()
        {
            var credentials = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
            var result = await credentials.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
            return result.Token;
        }






    }

}
