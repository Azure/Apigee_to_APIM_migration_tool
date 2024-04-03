using ApigeeToApimMigrationTool.Core.Config;
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
        private readonly IBundleProvider _bundleProvider;
        private readonly IExpressionTranslator _expressionTranslator;
        public PolicyTransformationFactory(IApigeeManagementApiService apigeeService, IApimProvider apimProvider, IBundleProvider bundleProvider, IApigeeXmlLoader apigeeXmlLoader, IExpressionTranslator expressionTranslator)
        {
            _apigeeService = apigeeService;
            _apimProvider = apimProvider;
            _apigeeXmlLoader = apigeeXmlLoader;
            _bundleProvider = bundleProvider;
            _expressionTranslator = expressionTranslator;
        }
        public IPolicyTransformation GetTransformationForPolicy(string policyName, IList<KeyValuePair<string, string>> policyVariables, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig)
        {
            switch (policyName)
            {
                case "AssignMessage":
                    return new AssignMessageTransformation(_expressionTranslator);
                case "LookupCache":
                    return new LookupCacheTransformation(policyVariables);
                case "KeyValueMapOperations":
                    return new KeyValueMapTransformation(_apimProvider, _apigeeService, policyVariables);
                case "VerifyJWT":
                    return new VerifyJwtTransformation(policyVariables);
                case "ServiceCallout":
                    return new ServiceCalloutTransformation(_apigeeService, _apimProvider, _expressionTranslator);
                case "ExtractVariables":
                    return new ExtractVariablesTransformation(policyVariables);
                case "OAuthV2":
                    return new OAuthV2Transformation(apimConfig);
                case "PopulateCache":
                    return new PopulateCacheTransformation(policyVariables);
                case "InvalidateCache":
                    return new InvalidateCacheTransformation();
                case "FlowCallout":
                    return new FlowCalloutTransformation(_apigeeXmlLoader, _apimProvider, _bundleProvider, _apigeeService, apimConfig, apigeeConfiguration);
                case "RaiseFault":
                    return new RaiseFaultTransformation(_expressionTranslator);
                case "BasicAuthentication":
                    return new BasicAuthenticationTransformation();
                case "SpikeArrest":
                    return new SpikeArrestTransformation(_expressionTranslator);
                case "AccessControl":
                    return new AccessControlTransformation();
                default:
                    return new NullTransformation();

            }
        }
    }
}
