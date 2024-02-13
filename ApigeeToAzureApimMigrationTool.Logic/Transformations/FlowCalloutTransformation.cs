using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class FlowCalloutTransformation : IPolicyTransformation
    {
        private readonly IApigeeXmlLoader _apigeeXmlLoader;
        private readonly IApimProvider _apimProvider;
        private readonly IApigeeManagementApiService _apiService;
        private readonly IBundleProvider _bundleProvider;

        private string _sharedFlowName;

        public FlowCalloutTransformation(IApigeeXmlLoader apigeeXmlLoader, IApimProvider apimProvider, IBundleProvider bundleProvider, IApigeeManagementApiService apiService)
        {
            _apigeeXmlLoader = apigeeXmlLoader;
            _apimProvider = apimProvider;
            _apiService = apiService;
            _bundleProvider = bundleProvider;
        }

        public async Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>();

            _sharedFlowName = element.Element("SharedFlowBundle").Value;
            apimPolicies.Add(IncludeFragment(_sharedFlowName));
            return apimPolicies.AsEnumerable();
        }

        public async Task DonwloadAndTransformSharedFlow(IApimPolicyTransformer transformer)
        {
            string sharedFlowBundlePath = await DownloadSharedFlow(_sharedFlowName);
            await ImportSharedFlow(_sharedFlowName, _apimProvider.ApimName, transformer);
        }

        private async Task<string> DownloadSharedFlow(string sharedFlowName)
        {
            var sharedFlowMetadata = await _apiService.GetSharedFlowByName(sharedFlowName);
            var bundle = _bundleProvider.GetSharedFlowBundle(sharedFlowName);
            return await _apiService.DownloadSharedFlowBundle(bundle.GetBundlePath(), sharedFlowName, sharedFlowMetadata.revision.Select(x => int.Parse(x)).Max());
        }

        private async Task ImportSharedFlow(string sharedflowName, string apimName, IApimPolicyTransformer apimPolicyTransformer)
        {
            var rawPolicyFragment = RawPolicyFragmentXml();

            var sharedFlowBundleXml = _apigeeXmlLoader.LoadSharedFlowBundleXml(sharedflowName);
            var sharedFlowElement = sharedFlowBundleXml.Element("SharedFlowBundle");
            string sharedFlowName = sharedFlowElement.Attribute("name").Value;
            string displayName = sharedFlowElement.Element("DisplayName").Value;
            string description = sharedFlowElement.Element("Description").Value;

            var sharedFlows = sharedFlowElement.Element("SharedFlows").Elements("SharedFlow");

            foreach (var sharedFlow in sharedFlows)
            {
                var sharedFlowXml = _apigeeXmlLoader.LoadSharedFlowXml(sharedFlowName, sharedFlow.Value);
                var sharedFlowRootElement = sharedFlowXml.Element("SharedFlow");
                var steps = sharedFlowRootElement.Elements("Step");

                await apimPolicyTransformer.TransformPoliciesInCollection(steps, rawPolicyFragment.Root, _apigeeXmlLoader.LoadSharedFlowPolicyXml, apimName, sharedFlowName);
                await _apimProvider.CreatePolicyFragment(sharedFlowName, apimName, WebUtility.HtmlDecode(rawPolicyFragment.ToString()), description);
            }
        }

        private XElement IncludeFragment(string fragmentName)
        {
            return new XElement("include-fragment", new XAttribute("fragment-id", fragmentName));
        }

        private XDocument RawPolicyFragmentXml()
        {
            string rawFragment = @"<fragment></fragment>";

            return XDocument.Parse(rawFragment);
        }



    }
}
