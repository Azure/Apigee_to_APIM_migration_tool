using Azure.ResourceManager.ApiManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IApimProvider
    {
        Task<ApiResource> CreateApi(string apiName, string apiDisplayName, string apiDescription, string apimName,
            string revision, string apiPath, string backendUrl, string? oauthConfigurationName);
        Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string apimName);
        Task CreatePolicyFragment(string policyFragmentName, string apimName, string policyFragmentXml, string policyFragmentDescription);
        Task CreatePolicy(XDocument? policyXml);
        Task CreateOrUpdateOperation(string apiName, string description, string httpVerb);
        Task CreateOrUpdateOperationPolicy(XDocument operationPolicyXml, string operationName, string operationDescription, string httpVerb, string proxyPath);
        Task AddApiToProduct(string apiId);

    }
}
