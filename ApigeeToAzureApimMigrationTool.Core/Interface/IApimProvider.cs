using Azure.ResourceManager.ApiManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IApimProvider
    {
        Task<ApiResource> CreateApi(string apiName, string apiDisplayName, string apiDescription, string apimName,
            string revision, string apiPath, string backendUrl, string? oauthConfigurationName);
        Task CreatePolicyFragment(string policyFragmentName, string apimName, string policyFragmentXml, string policyFragmentDescription);
        Task<ApiManagementProductResource> CreateProduct(string name, string displayName, string description, string apimName);
    }
}
