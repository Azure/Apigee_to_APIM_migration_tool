using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class PolicyTransformationFactory : IPolicyTransformationFactory
    {
        private readonly IApigeeXmlLoader _apigeeXmlLoader;
        private readonly IApigeeManagementApiService _apigeeService;
        private readonly IApimProvider _apimProvider;
        public PolicyTransformationFactory(IApigeeManagementApiService apigeeService, IApimProvider apimProvider, IApigeeXmlLoader apigeeXmlLoader)
        {
            _apigeeService = apigeeService;
            _apimProvider = apimProvider;
            _apigeeXmlLoader = apigeeXmlLoader;
        }
        public IPolicyTransformation GetTransformationForPolicy(string policyName, IList<KeyValuePair<string, string>> policyVariables)
        {
            switch (policyName)
            {
                case "AssignMessage":
                    return new AssignMessageTransformation();
                case "LookupCache":
                    return new LookupCacheTransformation(policyVariables);
                case "KeyValueMapOperations":
                    return new KeyValueMapTransformation(_apimProvider, _apigeeService, policyVariables);
                case "VerifyJWT":
                    return new VerifyJwtTransformation(policyVariables);
                case "ServiceCallout":
                    return new ServiceCalloutTransformation(_apigeeService, _apimProvider);
                case "ExtractVariables":
                    return new ExtractVariablesTransformation(policyVariables);
                case "OAuthV2":
                    return new OAuthV2Transformation();
                case "PopulateCache":
                    return new PopulateCacheTransformation(policyVariables);
                case "FlowCallout":
                    return new FlowCalloutTransformation(_apigeeXmlLoader, _apimProvider, _apigeeService);
                default:
                    return new NullTransformation();

            }
        }
    }
}
