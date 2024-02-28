using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class AssignMessageTransformation : IPolicyTransformation
    {
        private readonly IExpressionTranslator _expressionTranslator;

        public AssignMessageTransformation(IExpressionTranslator expressionTranslator)
        {
            _expressionTranslator = expressionTranslator;
        }

        /// <summary>
        /// Transforms the Apigee policy element into a collection of Azure API Management policies.
        /// </summary>
        /// <param name="apigeePolicyElement">The Apigee policy element to transform.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <returns>A task that represents the asynchronous transformation operation. The task result contains the collection of transformed Azure API Management policies.</returns>
        public virtual Task<IEnumerable<XElement>> Transform(XElement apigeePolicyElement, string apigeePolicyName)
        {
            return Task.FromResult(BuildApimPolicyCollection(apigeePolicyElement).AsEnumerable());
        }

        /// <summary>
        /// Transforms the Apigee policy element into a collection of Azure API Management policies.
        /// </summary>
        /// <param name="apigeePolicyElement">The Apigee policy element to transform.</param>
        /// <returns>A collection of transformed Azure API Management policies.</returns>
        internal IEnumerable<XElement> BuildApimPolicyCollection(XElement apigeePolicyElement)
        {
            var apimPolicies = new List<XElement>();

            if (apigeePolicyElement.Element("Add")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Add").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, AssignMessagePolicyOperations.Add));
                }
            }

            if (apigeePolicyElement.Element("Set")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Set").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, AssignMessagePolicyOperations.Set));
                }
            }

            if (apigeePolicyElement.Element("Remove")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Remove").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, AssignMessagePolicyOperations.Remove));
                }
            }

            if (apigeePolicyElement.Element("Set")?.Element("Payload") != null)
            {
                var (setBodyPolicy, contentType) = SetBody(apigeePolicyElement.Element("Set").Element("Payload"));
                apimPolicies.Add(setBodyPolicy);
                if (contentType != null)
                {
                    apimPolicies.Add(new XElement("set-header", new XAttribute("name", "Content-Type"), new XAttribute("exists-action", "override"), new XElement("value", contentType)));
                }
            }

            if (apigeePolicyElement.Element("AssignVariable") != null)
            {
                apimPolicies.Add(SetVariable(apigeePolicyElement));
            }

            return apimPolicies;
        }

        /// <summary>
        /// Sets the body of the policy and returns the transformed policy element along with the content type.
        /// </summary>
        /// <param name="body">The body element of the policy.</param>
        /// <returns>A tuple containing the transformed policy element and the content type.</returns>
        private (XElement, string?) SetBody(XElement body)
        {
            string? contentType = body.Attribute("contentType")?.Value;

            var value = body.Value.Trim();

            var newPolicy = new XElement("set-body");

            if (_expressionTranslator.ContentHasVariablesInIt(value))
            {
                if (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring(1, value.Length - 2);

                const string apigeeVariablePattern = @"{(.*?)}";

                foreach (Match match in Regex.Matches(value, apigeeVariablePattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var translatedExpression = _expressionTranslator.TranslateSingleItem(match.Groups[1].Value);

                        string? apimLiquidVariable = default;
                        if (translatedExpression == match.Groups[1].Value)
                             apimLiquidVariable = "{{" + $"context.Variables[\"{translatedExpression}\"]" + "}}";
                        else
                            apimLiquidVariable =  translatedExpression;

                        value.Replace(match.Groups[0].Value, apimLiquidVariable);
                    }
                }
            }

            // Set the value of the new policy element to the transformed body content
            newPolicy.Value = value;

            // Return the transformed policy element and the content type as a tuple
            return (newPolicy, contentType);
        }

        /// <summary>
        /// Sets the header of the policy based on the specified operation.
        /// </summary>
        /// <param name="header">The header element of the policy.</param>
        /// <param name="operation">The operation to perform on the header.</param>
        /// <returns>The transformed policy element representing the header.</returns>
        private XElement SetHeader(XElement header, AssignMessagePolicyOperations operation)
        {
            var name = header.Attribute("name")?.Value;
            var value = header.Value;

            const string apigeeVariablePattern = @"{(.*?)}";

            // Iterate through all matches of the pattern in the body content
            foreach (Match match in Regex.Matches(value, apigeeVariablePattern))
            {
                if (match.Success && match.Groups.Count > 0)
                {
                    // Translate the Apigee variable expression to Azure API Management liquid syntax
                    var translatedExpression = _expressionTranslator.TranslateSingleItem(match.Groups[1].Value);
                    if (translatedExpression == match.Groups[1].Value)
                    {
                        value = value.Replace(match.Groups[0].Value, $"@(context.Variables.GetValueOrDefault<string>(\"{translatedExpression}\"))");
                    }
                }
            }

            if (operation == AssignMessagePolicyOperations.Add)
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "skip"), new XElement("value", WebUtility.HtmlDecode(value)));
            else if (operation == AssignMessagePolicyOperations.Remove)
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "delete"));
            else
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "override"), new XElement("value", WebUtility.HtmlDecode(value)));
        }

        /// <summary>
        /// Sets the variable of the policy.
        /// </summary>
        /// <param name="assignMessageElement">The AssignVariable element of the policy.</param>
        /// <returns>The transformed policy element representing the variable.</returns>
        private XElement SetVariable(XElement assignMessageElement)
        {
            var assignVariable = assignMessageElement.Element("AssignVariable");
            var name = assignVariable.Element("Name")?.Value;

            var value = assignVariable.Element("Value")?.Value;
            var template = assignVariable.Element("Template")?.Value;
            var refValue = assignVariable.Element("Ref")?.Value;

            XElement newPolicy;
            if (value != null && template == null && refValue == null)
            {
                // Simple case, just name and value
                newPolicy = new XElement("set-variable", new XAttribute("name", name), new XAttribute("value", value));
                return newPolicy;
            }

            // Having Ref and Template doesn't make sense (at least I don't think it does...)
            if (refValue != null && template != null)
            {
                throw new Exception("Having both Ref and Template elements in AssignVariable is not supported.");
            }

            var finalValue = string.Empty;

            if (refValue != null)
            {
                refValue = _expressionTranslator.TranslateWholeString(refValue);
                finalValue = refValue;
            }
            if (template != null)
            {
                template = template.Trim();
                template = _expressionTranslator.TranslateWholeString(template);
                var apimTemplateBuilder = new StringBuilder();
                for (int i = 0; i < template.Length; i++)
                {
                    if (template[i] == '{')
                    {
                        if (i > 0 && template[i - 1] != '}')
                        {
                            apimTemplateBuilder.Append("\" + ");
                        }
                    }
                    else if (template[i] == '}')
                    {
                        if (i < template.Length - 1)
                        {
                            apimTemplateBuilder.Append(" + ");
                            if (template[i + 1] != '{')
                            {
                                apimTemplateBuilder.Append('"');
                            }
                        }
                    }
                    else
                    {
                        apimTemplateBuilder.Append(template[i]);
                    }
                }

                finalValue = apimTemplateBuilder.ToString();
            }

            // Value is the default value if the Ref variable is not defined
            if (value != null)
            {
                finalValue = $"{finalValue} ? {finalValue} : \"{value}\"";
            }

            newPolicy = new XElement("set-variable", new XAttribute("name", name), new XAttribute("value", $"@({finalValue})"));
            return newPolicy;
        }
    }
}
