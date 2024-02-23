using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
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
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicyElements = new List<XElement>();
            apimPolicyElements.AddRange(ExtractJsonValue(element, apigeePolicyName, policyDirection));
            return Task.FromResult(apimPolicyElements.AsEnumerable());
        }

        /// <summary>
        /// Extracts the JSON value from the XML element and creates a new Azure API Management policy element.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee policy.</param>
        /// <param name="policyName">The name of the Apigee policy.</param>
        /// <returns>The new Azure API Management policy element.</returns>
        private List<XElement> ExtractJsonValue(XElement element, string policyName, PolicyDirection policyDirection)
        {
            List<XElement> policies = new List<XElement>();
            var variablePrefix = element.Element("VariablePrefix") == null ? string.Empty : element.Element("VariablePrefix").Value;
            string sourceName = element.Element("Source")?.Value;
            bool isSourceVariable = false;

            if (string.IsNullOrEmpty(sourceName))
            {
                if (policyDirection == PolicyDirection.Inbound)
                    sourceName = "Request";
                else if (policyDirection == PolicyDirection.Outbound)
                    sourceName = "Response";
            }
            else
            {
                if (!sourceName.Equals("request") && !sourceName.Equals("response"))
                    isSourceVariable = true;
            }


            if (element.Element("JSONPayload") != null)
            {
                var payloadElement = element.Element("JSONPayload");
                foreach (var variableElement in payloadElement.Elements("Variable"))
                {
                    string translatedExpression = string.Empty;
                    if (isSourceVariable)
                    {
                        translatedExpression = "context.Variables.GetValueOrDefault<string>(\"" + sourceName + "\")";
                    }
                    else
                    {
                        translatedExpression = $"context.{sourceName}.Body.As<string>(preserveContent: true)";
                    }

                    var variableName = variableElement.Attribute("name").Value;
                    var jsonPath = variableElement.Element("JSONPath").Value;

                    string apimExpression = "@{" +
                            $"JObject json = JObject.Parse({translatedExpression});" +
                            "var extractedValue = json.SelectToken(\"" + jsonPath + "\");" +
                            "return extractedValue;" +
                            "}";
                    string apimVariableName = string.IsNullOrEmpty(variablePrefix) ? variableName : $"{variablePrefix}.{variableName}";
                    policies.Add(new XElement("set-variable", new XAttribute("name", apimVariableName), new XAttribute("value", apimExpression)));
                    _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
                }
            }
            else if (element.Element("Header") != null)
            {
                string headerName = element.Element("Header").Attribute("name").Value;
                bool IgnoreCaseInPattern = Convert.ToBoolean(element.Element("Header").Element("Pattern").Attribute("ignoreCase").Value);
                string patternValue = element.Element("Header").Element("Pattern").Value;

                string patternRegex = @"{(.*?)}";
                foreach (Match match in Regex.Matches(patternValue, patternRegex))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        string variableName = match.Groups[1].Value;
                        string variablePattern = match.Groups[0].Value;
                        string apimExpression = "@{" +
                                 $"string headerValue = \"\"; string pattern=\"{patternValue}\"" +
                                $"headerValue = context.Request.Headers.GetValueOrDefault(\"{headerName}\");" +
                                $"headerValue = headerValue.Replace(pattern.Replace(\"{variablePattern}\", \"\"),\"\");" +
                                "}";
                        string apimVariableName = string.IsNullOrEmpty(variablePrefix) ? variableName : $"{variablePrefix}.{variableName}";
                        policies.Add(new XElement("set-variable", new XAttribute("name", apimVariableName), new XAttribute("value", apimExpression)));
                    }
                }
            }
            else if (element.Elements("QueryParam") != null)
            {

            }
            else if (element.Elements("XMLPayload") != null)
            {

            }

            return policies;
        }
    }
}
