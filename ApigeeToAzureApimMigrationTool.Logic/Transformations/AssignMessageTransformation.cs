using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class AssignMessageTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement apigeePolicyElement, string apigeePolicyName)
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
                if(contentType != null)
                {
                    apimPolicies.Add(new XElement("set-header", new XAttribute("name", "Content-Type"), new XAttribute("exists-action", "override"), new XElement("value", contentType)));
                }
            }

            if (apigeePolicyElement.Element("AssignVariable") != null)
            {
                apimPolicies.Add(SetVariable(apigeePolicyElement));
            }

            return Task.FromResult(apimPolicies.AsEnumerable());

        }

        private (XElement,string?) SetBody(XElement body)
        {
            string? contentType = body.Attribute("contentType")?.Value;
            var value = body.Value.Trim();
            var newPolicy = new XElement("set-body");
            var expressionTranslator = new ExpressionTranslator();

            if (expressionTranslator.ContentHasVariablesInIt(value))
            {
                newPolicy.Add(new XAttribute("template", "liquid"));
                if (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring(1, value.Length - 2);

                const string apigeeVariablePattern = @"{(.*?)}";
                foreach (Match match in Regex.Matches(value, apigeeVariablePattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var translatedExpression = expressionTranslator.TranslateSingleItem(match.Groups[1].Value);
                        var apimLiquidVariable = "{{" + translatedExpression + "}}";
                        value.Replace(match.Groups[0].Value, apimLiquidVariable);
                    }
                }

            }

            newPolicy.Value = value;
            return (newPolicy, contentType);
        }

        private XElement SetHeader(XElement header, AssignMessagePolicyOperations operation)
        {
            var name = header.Attribute("name")?.Value;
            var value = header.Value;
            if (value.StartsWith('{'))
            {
                value = value.Replace("{", "").Replace("}", "");
                var expressionTranslator = new ExpressionTranslator();
                value = expressionTranslator.TranslateWholeString(value);
                value = WebUtility.HtmlDecode($"@({value})");
            }

            if (operation == AssignMessagePolicyOperations.Add)
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "skip"), new XElement("value", value));
            else if (operation == AssignMessagePolicyOperations.Remove)
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "delete"));
            else
                return new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", "override"), new XElement("value", value));
        }

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

            var expressionTranslator = new ExpressionTranslator();
            var finalValue = string.Empty;

            if (refValue != null)
            {
                refValue = expressionTranslator.TranslateWholeString(refValue);
                finalValue = refValue;
            }
            if (template != null)
            {
                template = template.Trim();
                template = expressionTranslator.TranslateWholeString(template);
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
