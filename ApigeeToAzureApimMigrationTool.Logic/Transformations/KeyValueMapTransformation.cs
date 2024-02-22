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
    /// <summary>
    /// Represents a transformation for converting Apigee key-value map policies to Azure API Management policies.
    /// </summary>
    public class KeyValueMapTransformation : IPolicyTransformation
    {
        private readonly IApimProvider _apimProvider;
        private readonly IApigeeManagementApiService _apigeeService;
        private readonly IList<KeyValuePair<string, string>> _policyVariables;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyValueMapTransformation"/> class.
        /// </summary>
        /// <param name="apimProvider">The Azure API Management provider.</param>
        /// <param name="apigeeService">The Apigee management API service.</param>
        /// <param name="policyVariables">The list of policy variables.</param>
        public KeyValueMapTransformation(IApimProvider apimProvider, IApigeeManagementApiService apigeeService, IList<KeyValuePair<string, string>> policyVariables)
        {
            _apimProvider = apimProvider;
            _apigeeService = apigeeService;
            _policyVariables = policyVariables;
        }

        /// <summary>
        /// Transforms the given Apigee element into a collection of Azure API Management policies.
        /// </summary>
        /// <param name="apigeeElement">The Apigee element to transform.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <returns>A collection of Azure API Management policies as XElement objects.</returns>
        public async Task<IEnumerable<XElement>> Transform(XElement apigeeElement, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>();

            foreach (var setVariableElement in await SetVariable(apigeeElement, _apigeeService.ProxyName, _apimProvider.ApimName, apigeePolicyName))
                apimPolicies.Add(setVariableElement);

            return apimPolicies.AsEnumerable();
        }

        /// <summary>
        /// Sets the variable values based on the Apigee element.
        /// </summary>
        /// <param name="element">The Apigee element to process.</param>
        /// <param name="proxyName">The name of the proxy.</param>
        /// <param name="apimName">The name of the Azure API Management instance.</param>
        /// <param name="policyName">The name of the policy.</param>
        /// <returns>A collection of Azure API Management policies as XElement objects.</returns>
        /// only Get operation is supported by APIM. Put and Delete is not supported.
        private async Task<IEnumerable<XElement>> SetVariable(XElement element, string proxyName, string apimName, string policyName)
        {
            XDocument setVariablePolicies = new XDocument();
            setVariablePolicies.Add(new XElement("Root"));
            var mapIdentifier = element.Attribute("mapIdentifier").Value;

            var getElements = element.Elements("Get");
            if (getElements != null)
                foreach (var getElement in getElements)
                {
                    // assignTo not yet supported
                    var assignTo = getElement.Attribute("assignTo");
                    if (assignTo != null)
                    {
                        Console.WriteLine("WARNING: assignTo is not supported for Key Value Maps.");
                        return new List<XElement>();
                    }
                    var index = getElement.Attribute("index").Value;
                    var key = getElement.Element("Key").Element("Parameter").Value;
                    var variableName = getElement.Attribute("assignTo").Value;


                    var apigeeKeyValueMap = await _apigeeService.GetKeyValueMapByName(proxyName, _apigeeService.Environment, mapIdentifier);
                    if (apigeeKeyValueMap != null)
                    {
                        var keyValueMapEntry = apigeeKeyValueMap.Entry.FirstOrDefault(x => x.Name.Equals(key));
                        if (keyValueMapEntry == null)
                            throw new Exception($"Can't find entry {key} under mapIdentifier {mapIdentifier} in Apigee");

                        for (int i = 0; i < apigeeKeyValueMap.Entry.Count; i++)
                        {
                            var entry = apigeeKeyValueMap.Entry.ElementAt(i);
                            await _apimProvider.AddNamedValue(apimName, proxyName, mapIdentifier, key, apigeeKeyValueMap.Encrypted, entry.Value, i+1);
                        }
                    }

                    string namedValueName = $"{mapIdentifier}-{key}-{index}"; 
                    namedValueName = namedValueName.Replace("_", "-");

                    var policy = new XElement("set-variable", new XAttribute("name", variableName), new XAttribute("value", "{{" + namedValueName + "}}"));
                    _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
                    setVariablePolicies.Root.Add(policy);
                }
            return setVariablePolicies.Root.Elements();

        }

    }
}
