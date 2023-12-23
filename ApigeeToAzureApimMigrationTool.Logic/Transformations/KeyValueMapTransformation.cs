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
    public class KeyValueMapTransformation : IPolicyTransformation
    {
        private readonly IApimProvider _apimProvider;
        private readonly IApigeeManagementApiService _apigeeService;

        private readonly IList<KeyValuePair<string, string>> _policyVariables;
        public KeyValueMapTransformation(IApimProvider apimProvider, IApigeeManagementApiService apigeeService, IList<KeyValuePair<string, string>> policyVariables)
        {
            _apimProvider = apimProvider;
            _apigeeService = apigeeService;
            _policyVariables = policyVariables;
        }

        public async Task<IEnumerable<XElement>> Transform(XElement apigeeElement, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>();

            foreach (var setVariableElement in await SetVariable(apigeeElement, _apigeeService.ProxyName, _apimProvider.ApimName, apigeePolicyName))
                apimPolicies.Add(setVariableElement);

            return apimPolicies.AsEnumerable();
        }

        private async Task<IEnumerable<XElement>> SetVariable(XElement element, string proxyName, string apimName, string policyName)
        {
            XDocument setVariablePolicies = new XDocument();
            setVariablePolicies.Add(new XElement("Root"));
            var mapIdentifier = element.Attribute("mapIdentifier").Value;

            var getElements = element.Elements("Get");
            if (getElements != null)
                foreach (var getElement in getElements)
                {
                    var index = getElement.Attribute("index").Value;
                    var key = getElement.Element("Key").Element("Parameter").Value;
                    var variableName = getElement.Attribute("assignTo").Value;


                    var apigeeKeyValueMap = await _apigeeService.GetKeyValueMapByName(proxyName, _apigeeService.Environment, mapIdentifier);
                    if (apigeeKeyValueMap != null)
                    {
                        var keyValueMapEntry = apigeeKeyValueMap.Entry.FirstOrDefault(x => x.Name.Equals(key));
                        if (keyValueMapEntry == null)
                            throw new Exception($"Can't find entry {key} under mapIdentifier {mapIdentifier} in Apigee");
                        await _apimProvider.AddNamedValue(apimName, proxyName, mapIdentifier, key, apigeeKeyValueMap.Encrypted, keyValueMapEntry.Value);
                    }

                    string namedValueName = $"{mapIdentifier}-{key}";
                    namedValueName = namedValueName.Replace("_", "-");

                    var policy = new XElement("set-variable", new XAttribute("name", variableName), new XAttribute("value", "{{" + namedValueName + "}}"));
                    _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
                    setVariablePolicies.Root.Add(policy);
                }
            return setVariablePolicies.Root.Elements();

        }

    }
}
