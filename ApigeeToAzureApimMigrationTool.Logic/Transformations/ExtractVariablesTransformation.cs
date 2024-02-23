using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class ExtractVariablesTransformation : IPolicyTransformation
    {
        private readonly IList<KeyValuePair<string, string>> _policyVariables;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractVariablesTransformation"/> class.
        /// </summary>
        /// <param name="policyVariables">The list of policy variables.</param>
        public ExtractVariablesTransformation(IList<KeyValuePair<string, string>> policyVariables)
        {
            _policyVariables = policyVariables;
        }

        /// <summary>
        /// Transforms the given XML element into a collection of Azure API Management policy elements.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee policy.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <returns>A task that represents the asynchronous transformation operation.</returns>
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            var apimPolicyElements = new List<XElement>();
            if (element.Element("JSONPayload") != null)
            {
                apimPolicyElements.Add(ExtractJsonValue(element, apigeePolicyName));
            }

            return Task.FromResult(apimPolicyElements.AsEnumerable());
        }

        /// <summary>
        /// Extracts the JSON value from the XML element and creates a new Azure API Management policy element.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee policy.</param>
        /// <param name="policyName">The name of the Apigee policy.</param>
        /// <returns>The new Azure API Management policy element.</returns>
        private XElement ExtractJsonValue(XElement element, string policyName)
        {
            if (element.Element("JSONPayload") != null)
            {
                var jsonVariableName = element.Element("Source").Value;
                var variablePrefix = element.Element("VariablePrefix").Value;
                var variableName = element.Element("JSONPayload").Element("Variable").Attribute("name").Value;
                var jsonPath = element.Element("JSONPayload").Element("Variable").Element("JSONPath").Value.Replace("$", "");

                string apimExpression = "@{" +
                        "JObject json = JObject.Parse(context.Variables.GetValueOrDefault<string>(\"" + jsonVariableName + "\"));" +
                        "var typeCode = json.GetValue(\"" + jsonPath.Replace(".", "").Trim() + "\");" +
                        "return typeCode;" +
                        "}";

                var newPolicy = new XElement("set-variable", new XAttribute("name", $"{variablePrefix}.{variableName}"), new XAttribute("value", apimExpression));
                _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
                return newPolicy;
            }
            else if (element.Element("Header") != null)
            {

            }
            else if (element.Elements("QueryParam") != null)
            {

            }
            else if (element.Elements("XMLPayload") != null)
            {

            }
        }
    }
}
