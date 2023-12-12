using ApigeeToApimMigrationTool.Test.AzureSdkMocks;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using Azure.ResourceManager.ApiManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToApimMigrationTool.Test
{
    public class MockApimProvider : IApimProvider
    {
        public XDocument PolicyXml { get; private set; } = new XDocument();

        public Task AddApiToProduct(string apiId)
        {
            return Task.CompletedTask;
        }

        public Task AddNamedValue(string apimName, string proxyName, string mapIdentifier, string keyName, bool isSecret, string value, string keyVaultName)
        {
            return Task.CompletedTask;
        }

        public Task<ApiResource> CreateApi(string apiName, string apiDisplayName, string apiDescription, string apimName, string revision, string apiPath, string backendUrl, string? oauthConfigurationName)
        {
            var mockResource = new MockApiResource();
            return Task.FromResult(mockResource as ApiResource);
        }

        public Task CreateOrUpdateOperation(string apiName, string description, string httpVerb)
        {
            return Task.CompletedTask;
        }

        public Task CreateOrUpdateOperationPolicy(XDocument operationPolicyXml, string operationName, string operationDescription, string httpVerb, string proxyPath)
        {
            return Task.CompletedTask;
        }

        public Task CreatePolicy(XDocument policyXml)
        {
            PolicyXml = policyXml;
            return Task.CompletedTask;
        }

        public Task CreatePolicyFragment(string policyFragmentName, string apimName, string policyFragmentXml, string policyFragmentDescription)
        {
            return Task.CompletedTask;
        }

        public Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string apimName)
        {
            var mockProduct = new MockApiManagementProductResource();
            return Task.FromResult(mockProduct as ApiManagementProductResource);
        }
    }
}
