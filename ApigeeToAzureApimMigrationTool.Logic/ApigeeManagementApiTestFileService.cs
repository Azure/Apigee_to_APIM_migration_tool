using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service.Bundles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ApigeeManagementApiTestFileService : IApigeeManagementApiService
    {
        private readonly IBundleProvider _apigeeBundleProvider;
        private readonly IApigeeXmlLoader _apigeeXmlLoader;
        private readonly string _localConfigPath;
        public ApigeeManagementApiTestFileService(IBundleProvider apigeeBundleProvider, IApigeeXmlLoader apigeeXmlLoader, string localConfigPath)
        {
            _apigeeBundleProvider = apigeeBundleProvider;
            _apigeeXmlLoader = apigeeXmlLoader;
            _localConfigPath = localConfigPath;
        }
        public string AuthenticationToken { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string? Environment { get; set; }

        public string ProxyName { get; set; }

        public Task<ApigeeEntityModel> GetApiProxyByName(string proxyName)
        {
            // For testing, do things backwards.  Get the metadata from the XML that we've 
            // already downloaded.
            XDocument proxyXml = _apigeeXmlLoader.LoadProxyXml(proxyName);
            if (proxyXml.Root == null)
            {
                throw new Exception($"Could not get xml for proxy {proxyName} from file.");
            }
        
            XElement? apiProxyElement = proxyXml.Root.Element("APIProxy");
            if (apiProxyElement == null)
            {
                throw new Exception($"Could not find APIProxy element in xml for proxy {proxyName}.");
            }

            var apiProxyModel = new ApigeeEntityModel();
            apiProxyModel.name = apiProxyElement.Attribute("name")?.Value ?? string.Empty;
            
            string revision = apiProxyElement.Attribute("revision")?.Value ?? string.Empty;
            apiProxyModel.revision = new string[] { revision };

            apiProxyModel.metaData = new ApiProxyMetaData();
            apiProxyModel.metaData.createdAt = long.Parse(apiProxyElement.Element("CreatedAt")?.Value ?? string.Empty);
            apiProxyModel.metaData.createdBy = apiProxyElement.Element("CreatedBy")?.Value ?? string.Empty;
            apiProxyModel.metaData.lastModifiedAt = long.Parse(apiProxyElement.Element("LastModifiedAt")?.Value ?? string.Empty);
            apiProxyModel.metaData.lastModifiedBy = apiProxyElement.Element("LastModifiedBy")?.Value ?? string.Empty;
            
            apiProxyModel.metaData.subType = string.Empty;

            return Task.FromResult(apiProxyModel);
        }

        public Task<ApigeeTargetServerModel> GetTargetServerByName(string targetServerName, string environment)
        {
            var defaultTargetServer = new ApigeeTargetServerModel
            {
                Host = targetServerName,
                IsEnabled = true,
                Name = targetServerName,
                Port = 8080,
                SSLInfo = new ApigeeTargetServerSSLInfo
                {
                    Ciphers = new string[] { "DEFAULT" },
                    ClientAuthEnabled = false,
                    Enabled = false,
                    IgnoreValidationError = false,
                    Protocols = new string[] { "TLSv1.2" },
                    TrustStore = "default"
                }
            };

            return Task.FromResult(defaultTargetServer);
        }

        public Task<string> DownloadApiProxyBundle(string basePath, string proxyName, int revision)
        {
            // Return the path of the already downloaded bundle
            var bundle = _apigeeBundleProvider.GetApiProxyBundle(proxyName);
            return Task.FromResult(bundle.GetBundlePath());
        }

        public Task<string> DownloadSharedFlowBundle(string basePath, string sharedFlowName, int revision)
        {
            // Return the path of the already downloaded shared flow bundle
            var bundle = _apigeeBundleProvider.GetSharedFlowBundle(sharedFlowName);
            return Task.FromResult(bundle.GetBundlePath());
        }

        public async Task<ApiProductMetaData> GetApiProductByName(string productName)
        {
            var defaultApiProductMetaData = new ApiProductMetaData
            {
                DisplayName = productName,
                Description = "Default",
                Name = productName,
                Proxies = new List<string>(),
            };

            // Iterate through the bundle directory and find all the proxies that are in the product
            var bundleDirectories = Directory.GetDirectories(_localConfigPath);
            foreach (var directory in bundleDirectories)
            {
                if (Directory.Exists($"{directory}{Path.DirectorySeparatorChar}apiproxy"))
                {
                    defaultApiProductMetaData.Proxies.Add(Path.GetFileName(directory));
                }
            }
            // END TODO

            // Load pre-configured product json file
            var apiProductFilePathBuilder = new StringBuilder($"{_localConfigPath}{Path.DirectorySeparatorChar}apiproducts{Path.DirectorySeparatorChar}{productName}.json");

            string apiProductFilePath = apiProductFilePathBuilder.ToString();

            if (!File.Exists(apiProductFilePath))
            {
                return defaultApiProductMetaData;
            }

            string apiProductJson = await File.ReadAllTextAsync(apiProductFilePath);
            var apiProductModel = JsonConvert.DeserializeObject<ApiProductMetaData>(apiProductJson);

            if (apiProductModel == null)
            {
                return defaultApiProductMetaData;
            }

            return apiProductModel;
        }


        public async Task<KeyValueMapModel> GetKeyValueMapByName(string proxyName, string environment, string mapIdentifier)
        {

            // Load pre-configured kvmap json file
            var keyValueMapFilePathBuilder = new StringBuilder($"{_localConfigPath}{Path.DirectorySeparatorChar}keyvaluemaps{Path.DirectorySeparatorChar}{mapIdentifier}");
            if (!string.IsNullOrEmpty(environment))
            {
                keyValueMapFilePathBuilder.Append($".{environment}");
            }
            keyValueMapFilePathBuilder.Append(".json");

            string keyValueMapPath = keyValueMapFilePathBuilder.ToString();

            if (!File.Exists(keyValueMapPath))
            {
                return null; // Same behavior as real service
            }

            string kvmapJson = await File.ReadAllTextAsync(keyValueMapPath);
            var kvmapModel = JsonConvert.DeserializeObject<KeyValueMapModel>(kvmapJson);

            return kvmapModel;
        }

        public Task<ApigeeEntityModel> GetSharedFlowByName(string sharedFlowName)
        {
            // Just like with the API proxy metadata, do things backwards.  Get the metadata from the XML that we've 
            // already downloaded.
            // HACK: We're going to assume we've downlaoded the "default.xml" file for the shared flow.
            XDocument proxyXml = _apigeeXmlLoader.LoadSharedFlowBundleXml(sharedFlowName);
            if (proxyXml.Root == null)
            {
                throw new Exception($"Could not get xml for shared flow {sharedFlowName} from file.");
            }

            XElement? sharedFlowBundleElement = proxyXml.Element("SharedFlowBundle");
            if (sharedFlowBundleElement == null)
            {
                throw new Exception($"Could not find SharedFlowBundle element in xml for shared flow {sharedFlowName}.");
            }

            var sharedFlowModel = new ApigeeEntityModel();
            sharedFlowModel.name = sharedFlowBundleElement.Attribute("name")?.Value ?? string.Empty;

            string revision = sharedFlowBundleElement.Attribute("revision")?.Value ?? string.Empty;
            sharedFlowModel.revision = new string[] { revision };

            sharedFlowModel.metaData = new ApiProxyMetaData();
            sharedFlowModel.metaData.createdAt = long.Parse(sharedFlowBundleElement.Element("CreatedAt")?.Value ?? string.Empty);
            sharedFlowModel.metaData.createdBy = sharedFlowBundleElement.Element("CreatedBy")?.Value ?? string.Empty;
            sharedFlowModel.metaData.lastModifiedAt = long.Parse(sharedFlowBundleElement.Element("LastModifiedAt")?.Value ?? string.Empty);
            sharedFlowModel.metaData.lastModifiedBy = sharedFlowBundleElement.Element("LastModifiedBy")?.Value ?? string.Empty;

            sharedFlowModel.metaData.subType = string.Empty;

            return Task.FromResult(sharedFlowModel);
        }


        public Task PopulateProxyReferenceDatabase()
        {
            throw new NotImplementedException();
        }
    }
}
