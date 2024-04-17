using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service;
using ApigeeToAzureApimMigrationTool.Service.Bundles;
using ApigeeToAzureApimMigrationTool.Service.Transformations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Test
{
    public class BundleLoaderTests
    {
        // The purpose of these tests is to test loading bundles from files.
        // We're using the test file loaders, but the process should be the same
        // with the real loaders, only we'll be loading from the filesystem instead 
        // of from the Apigee API.

        private readonly AzureApimService _azureApimServiceUnderTest;
        private readonly IBundleProvider _bundleProvider;
        private readonly MockApimProvider _apimProvider;
        private readonly IExpressionTranslator _expressionTranslator;

        public BundleLoaderTests()
        {
            var testConfigPath = "TestBundles";

            _bundleProvider = new ApigeeFileBundleProvider(testConfigPath);
            _apimProvider = new MockApimProvider();
            _expressionTranslator = new ExpressionTranslator();


            IApigeeXmlLoader apigeeXmlLoader = new ApigeeXmlFileLoader(_bundleProvider);
            IExpressionTranslator expressionTranslator = new ExpressionTranslator();
            IApigeeManagementApiService apigeeManagementApiService = new ApigeeManagementApiTestFileService(_bundleProvider, apigeeXmlLoader, testConfigPath);
            IPolicyTransformationFactory policyTransformationFactory = new PolicyTransformationFactory(apigeeManagementApiService, _apimProvider, _bundleProvider, apigeeXmlLoader, expressionTranslator);

            _azureApimServiceUnderTest = new AzureApimService(
                apigeeXmlLoader: apigeeXmlLoader,
                apimProvider: _apimProvider,
                expressionTranslator: _expressionTranslator,
                policyTransformer: new ApigeeToApimPolicyTransformer(policyTransformationFactory, _apimProvider, _expressionTranslator));
        }
        [Fact]
        public async Task FileBundleLoader_WithSharedFlowPolicy_LoadsSharedFlow()
        {
            var bundle = _bundleProvider.GetApiProxyBundle("Test-API");
            await bundle.LoadBundle();
            await _azureApimServiceUnderTest.ImportApi("Test-Apim", "Test-API", null, null, null);

            // GetSharedFlow is a shared flow that is used by the Test-API proxy
            Assert.True(_apimProvider.PolicyFragments.ContainsKey("GetSharedFlow"));

        }
    }
}
