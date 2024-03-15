using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service;
using ApigeeToAzureApimMigrationTool.Service.Transformations;
using Newtonsoft.Json;
using System.Reflection;
using System.Xml.Linq;

namespace ApigeeToApimMigrationTool.Test
{
    public class AzureApimServiceTest
    {
        private IAzureApimService _azureApimServiceUnderTest;

        // Dependencies
        private readonly MockApigeeManagementApiService _mockApigeeManagementApiService;
        private readonly MockApigeeXmlLoader _mockApigeeXmlLoader;
        private readonly MockApimProvider _mockApimProvider;
        private readonly MockBundleProvider _mockBundleProvider;
        private readonly IExpressionTranslator _expressionTranslator;

        public AzureApimServiceTest()
        {
            _mockApigeeManagementApiService = new MockApigeeManagementApiService();
            _mockApigeeXmlLoader = new MockApigeeXmlLoader();
            _mockApimProvider = new MockApimProvider();
            _mockBundleProvider = new MockBundleProvider();
            _expressionTranslator = new ExpressionTranslator();

            // We need a real transformation factory and transformer here because we are testing the integration between the service
            // and the transformer
            var policyTransformationFactory = new PolicyTransformationFactory(
                _mockApigeeManagementApiService, _mockApimProvider, _mockBundleProvider, _mockApigeeXmlLoader, _expressionTranslator);

            var policyTransformer = new ApigeeToApimPolicyTransformer(policyTransformationFactory);

            _azureApimServiceUnderTest = new AzureApimService(
                apimProvider: _mockApimProvider, 
                apigeeXmlLoader: _mockApigeeXmlLoader,
                policyTransformer: policyTransformer);

            SetupDefaultPolicies();
        }

        [Fact]
        public async Task AssignMessagePolicy_InRequestPreFlow_WithSetHeader_SetsApimHeader_InInbound()
        {
            XElement testPolicyElement = XElement.Parse(@"<Request><Step><Name>test-policy</Name></Step></Request>");
            XElement? preFlowPolicyElement = _mockApigeeXmlLoader.ProxyEndpointXml["default"].Element("ProxyEndpoint")?.Element("PreFlow");

            Assert.NotNull(preFlowPolicyElement);
            preFlowPolicyElement?.Add(testPolicyElement);

            _mockApigeeXmlLoader.PolicyXml.Add("test-policy", XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                  <AssignMessage async=""false"" continueOnError=""false"" enabled=""true"" name=""test-policy"">
                    <DisplayName>Set Header</DisplayName>
                    <FaultRules/>
                    <Properties/>
                    <Set>
                        <Headers>
                            <Header name=""test-header"">test-value</Header>
                        </Headers>
                    </Set>
                    <IgnoreUnresolvedVariables>true</IgnoreUnresolvedVariables>
                    <AssignTo createNew=""false"" transport=""http"" type=""request""/>
                 </AssignMessage>"));

            await _azureApimServiceUnderTest.ImportApi(
                apimName: "testApi", 
                proxyName: "Test-Api",
                apimConfiguration: new ApimConfiguration(),
                apigeeConfiguration: new ApigeeConfiguration(), 
                keyVaultName: string.Empty);

            var policy = _mockApimProvider.PolicyXml;

            XElement? setHeaderPolicy = policy?.Element("policies")?.Element("inbound")?.Element("set-header");
            Assert.NotNull(setHeaderPolicy);
            Assert.Equal("test-header", setHeaderPolicy?.Attribute("name")?.Value);
            Assert.Equal("test-value", setHeaderPolicy?.Element("value")?.Value);

        }

        private void SetupDefaultPolicies()
        {
            _mockApigeeXmlLoader.ProxyXml.Add("Test-Api", XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                  <APIProxy revision=""4"" name=""Test-API"">
                    <Basepaths>/test</Basepaths>
                    <Description>Test API</Description>
                    <DisplayName>Test-API</DisplayName>
                    <Policies>
                        <Policy>test-policy</Policy>
                    </Policies>
                    <ProxyEndpoints>
                        <ProxyEndpoint>default</ProxyEndpoint>
                    </ProxyEndpoints>
                    <Resources/>
                    <Spec></Spec>
                    <TargetServers/>
                    <TargetEndpoints>
                        <TargetEndpoint>default</TargetEndpoint>
                    </TargetEndpoints>
                 </APIProxy>"));

            _mockApigeeXmlLoader.ProxyEndpointXml.Add("default", XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                  <ProxyEndpoint name=""default"">
                    <PreFlow />
                    <PostFlow />
                    <Flows/>
                    <HTTPProxyConnection>
                        <BasePath>/test</BasePath>
                    </HTTPProxyConnection>
                    <RouteRule name=""default"">
                        <TargetEndpoint>default</TargetEndpoint>
                    </RouteRule>
                  </ProxyEndpoint>"));

            _mockApigeeXmlLoader.TargetXml.Add("default", XDocument.Parse(
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
                  <TargetEndpoint name=""default"">
                    <PreFlow />
                    <PostFlow />
                    <Flows/>
                    <HTTPTargetConnection>
                        <URL>https://test.example.com</URL>
                    </HTTPTargetConnection>
                </TargetEndpoint>"));
        }
    }
}