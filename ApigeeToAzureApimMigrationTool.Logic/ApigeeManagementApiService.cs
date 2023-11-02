using ApigeeToApimMigrationTool.Core;
using ApigeeToApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Runtime.Intrinsics.X86;

namespace ApigeeToAzureApimMigrationTool.Service
{

    public class ApigeeManagementApiService : IApigeeManagementApiService
    {
        private readonly HttpClient _client;
        private readonly IProxyMetaDataDataAccess _proxyMetaDataDataAccess;
        public ApigeeManagementApiService(string apigeeManagementApiBaseUrl, string organizationName, IProxyMetaDataDataAccess proxyMetaDataDataAccess)
        {
            _client = new HttpClient();
            _client.BaseAddress = new Uri($"{apigeeManagementApiBaseUrl}/v1/organizations/{organizationName}/");
            _proxyMetaDataDataAccess = proxyMetaDataDataAccess;
        }


        /// <summary>
        /// Get bearer token
        /// </summary>
        /// <param name="oneTimeToken">one time token recieved from </param>
        /// <returns>Bearer token</returns>
        public async Task<string> GetAuthenticationToken(string oneTimeToken, string authenticationBaseUrl)
        {
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", "Basic ZWRnZWNsaTplZGdlY2xpc2VjcmV0");

            HttpResponseMessage authTokenResponse = await client.PostAsync($"{authenticationBaseUrl}/oauth/token?grant_type=password&passcode={oneTimeToken}", null);
            authTokenResponse.EnsureSuccessStatusCode();
            var authresponse = JsonConvert.DeserializeObject<AuthToken>(await authTokenResponse.Content.ReadAsStringAsync());

            string token = authresponse.access_token;

            return token;
        }

        public async Task<string> GetAuthenticationToken(string username, string password, string authenticationBaseUrl)
        {
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", "Basic ZWRnZWNsaTplZGdlY2xpc2VjcmV0");

            HttpResponseMessage authTokenResponse = await client.PostAsync($"{authenticationBaseUrl}/oauth/token?grant_type=password&username={username}&password={password}", null);
            authTokenResponse.EnsureSuccessStatusCode();
            var authresponse = JsonConvert.DeserializeObject<AuthToken>(await authTokenResponse.Content.ReadAsStringAsync());

            string token = authresponse.access_token;

            return token;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="proxyName"></param>
        /// <param name="bearerToken"></param>
        /// <returns></returns>
        public async Task<ApigeeEntityModel> GetApiProxyByName(string proxyName, string bearerToken)
        {
            ResetHttpClient(bearerToken);
            HttpResponseMessage apiProxyResponse = await _client.GetAsync($"apis/{proxyName}");
            apiProxyResponse.EnsureSuccessStatusCode();
            var apiMetaData = JsonConvert.DeserializeObject<ApigeeEntityModel>(await apiProxyResponse.Content.ReadAsStringAsync());
            return apiMetaData;
        }

        public async Task<ApiProductMetaData> GetApiProductByName(string productName, string bearerToken)
        {
            ResetHttpClient(bearerToken);
            HttpResponseMessage apiProxyResponse = await _client.GetAsync($"apiproducts/{productName}");
            apiProxyResponse.EnsureSuccessStatusCode();
            var apiMetaData = JsonConvert.DeserializeObject<ApiProductMetaData>(await apiProxyResponse.Content.ReadAsStringAsync());
            return apiMetaData;
        }

        public async Task<string> DownloadApiProxyBundle(string proxyName, int revision, string bearerToken)
        {
            ResetHttpClient(bearerToken);
            HttpResponseMessage apiRevisionResponse = await _client.GetAsync($"apis/{proxyName}/revisions/{revision}?format=bundle");

            Stream inputStream = await apiRevisionResponse.Content.ReadAsStreamAsync();
            string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string proxyPath = Directory.CreateDirectory(Path.Combine(basePath, proxyName)).FullName;
            string zipPath = Path.Combine(proxyPath, $"{proxyName}.zip");
            using (FileStream outputFileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            {
                inputStream.CopyTo(outputFileStream);
            }
            ZipFile.ExtractToDirectory(zipPath, proxyPath, true);
            return proxyPath;
        }


        //TODO: replace with a new model
        public async Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName, string bearerToken)
        {
            ResetHttpClient(bearerToken);
            HttpResponseMessage apiProxyResponse = await _client.GetAsync($"sharedflows/{sharedFlowName}");
            apiProxyResponse.EnsureSuccessStatusCode();
            var apiMetaData = JsonConvert.DeserializeObject<ApigeeEntityModel>(await apiProxyResponse.Content.ReadAsStringAsync());
            return apiMetaData;
        }

        public async Task<string> DownloadSharedFlowBundle(string sharedFlowName, int revision, string bearerToken)
        {
            ResetHttpClient(bearerToken);
            HttpResponseMessage apiRevisionResponse = await _client.GetAsync($"sharedflows/{sharedFlowName}/revisions/{revision}?format=bundle");

            Stream inputStream = await apiRevisionResponse.Content.ReadAsStreamAsync();
            string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string sharedFlowPath = Directory.CreateDirectory(Path.Combine(basePath, sharedFlowName)).FullName;
            string zipPath = Path.Combine(sharedFlowPath, $"{sharedFlowName}.zip");
            using (FileStream outputFileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            {
                inputStream.CopyTo(outputFileStream);
            }
            ZipFile.ExtractToDirectory(zipPath, sharedFlowPath, true);
            return sharedFlowPath;
        }

        public async Task PopulateProxyReferenceDatabase(string bearerToken)
        {
            if (!await _proxyMetaDataDataAccess.IsProxyMetadataTablePopulted())
            {
                ResetHttpClient(bearerToken);
                HttpResponseMessage proxiListResponse = await _client.GetAsync($"apis/");
                var proxyNames = JsonConvert.DeserializeObject<string[]>(await proxiListResponse.Content.ReadAsStringAsync());
                foreach (string proxy in proxyNames)
                {
                    HttpResponseMessage apiProxyResponse = await _client.GetAsync($"apis/{proxy}");
                    var proxyMetaData = JsonConvert.DeserializeObject<ApigeeEntityModel>(await apiProxyResponse.Content.ReadAsStringAsync());
                    foreach (string revision in proxyMetaData.revision)
                    {
                        HttpResponseMessage apiRevisionResponse = await _client.GetAsync($"apis/{proxy}/revisions/{revision}");
                        var proxyRevisionMetaData = JsonConvert.DeserializeObject<ApiProxyRevisionMetadata>(await apiRevisionResponse.Content.ReadAsStringAsync());
                        
                        proxyRevisionMetaData.ProxyEndpointPaths = new Dictionary<string, string>();
                        foreach (var endpoint in proxyRevisionMetaData.ProxyEndpoints)
                        {
                            HttpResponseMessage apiRevisionEndpointResponse = await _client.GetAsync($"apis/{proxy}/revisions/{revision}/proxies/{endpoint}");
                            var proxyRevisionEndpointMetaData = JsonConvert.DeserializeObject<ApiProxyEndpointMetadata>(await apiRevisionEndpointResponse.Content.ReadAsStringAsync());
                            proxyRevisionMetaData.ProxyEndpointPaths.Add(endpoint, proxyRevisionEndpointMetaData.Connection.BasePath);
                        }
                        await _proxyMetaDataDataAccess.InsertProxyRevisionMetaData(proxyRevisionMetaData);
                    }
                }
            }
        }


        private void ResetHttpClient(string bearerToken)
        {
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");

        }

    }
}