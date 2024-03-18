using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service.Transformations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ApigeeToApimPolicyTransformer : IApimPolicyTransformer
    {

        private readonly IPolicyTransformationFactory _policyTransformationFactory;
        private readonly List<KeyValuePair<string, string>> _policyVariables;

        public ApigeeToApimPolicyTransformer(IPolicyTransformationFactory policyTransformationFactory)
        {
            _policyTransformationFactory = policyTransformationFactory;
            _policyVariables = new List<KeyValuePair<string, string>>();
        }
        public async Task TransformPoliciesInCollection(IEnumerable<XElement>? elements, XElement azureApimPolicySection, Func<string, string, XDocument> xmlLoader,
            string apimName, string proxyName, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig)
        {
            if (elements == null)
            {
                return;
            }

            foreach (var element in elements)
            {
                if (element == null)
                {
                    continue;
                }

                string? policyName = element.Element("Name")?.Value;

                if (policyName == null)
                {
                    throw new Exception($"Cannot find Name element in policy xml: {element}");
                }

                XElement? conditionElement = element.Element("Condition");
                string? condition = conditionElement?.Value;

                if (condition == null)
                {
                    condition = string.Empty;
                }

                var policyXml = xmlLoader(proxyName, policyName);

                var rootElement = policyXml.Root;
                if (rootElement == null)
                {
                    throw new Exception($"Cannot find root element in policy xml: {policyXml}");
                }

                await TransformPolicy(rootElement, rootElement.Name.ToString(), azureApimPolicySection, apimName, condition, policyName, apigeeConfiguration, apimConfig);
            }

        }

        private async Task TransformPolicy(XElement element, string apigeePolicyName, XElement apimPolicyElement, string apimName, string condition, string apigeePolicyDisplayName,
            ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig)
        {
            var policyTransformation = _policyTransformationFactory.GetTransformationForPolicy(apigeePolicyName, _policyVariables, apigeeConfiguration, apimConfig);
            var apimPolicies = await policyTransformation.Transform(element, apigeePolicyDisplayName);
            foreach (var apimPolicy in apimPolicies)
            {
                var policyWithCondition = ApplyConditionToPolicy(condition, apimPolicy);
                apimPolicyElement.Add(policyWithCondition);
            }

            // Special handling for Shared Flows, for which additional policies need to be downloaded from Apigee
            // and then processed by this transformer entirely separately
            var flowCalloutTransformation = policyTransformation as FlowCalloutTransformation;
            if (flowCalloutTransformation != null)
            {
                await flowCalloutTransformation.DonwloadAndTransformSharedFlow(this);
            }
        }

        private XElement ApplyConditionToPolicy(string condition, XElement policy)
        {
            if (!string.IsNullOrEmpty(condition))
            {
                ExpressionTranslator expressionTranslator = new ExpressionTranslator();
                condition = expressionTranslator.TranslateWholeString(condition);

                string cacheLookupPattern = @"lookupcache.(.*?).cachehit";
                foreach (Match match in Regex.Matches(condition, cacheLookupPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var policyName = match.Groups[1].Value;
                        string completeExpression = $"lookupcache.{policyName}.cachehit";
                        string variableName = _policyVariables.FirstOrDefault(x => x.Key == policyName).Value;
                        condition = condition.Replace(completeExpression, $"context.Variables.ContainsKey(\"{variableName}\")");
                    }
                }

                string jwtValidPattern = @"jwt.(.*?).valid";
                foreach (Match match in Regex.Matches(condition, jwtValidPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var policyName = match.Groups[1].Value;
                        string completeExpression = $"jwt.{policyName}.valid";
                        string variableName = _policyVariables.FirstOrDefault(x => x.Key == policyName).Value;
                        condition = condition.Replace(completeExpression, $"context.Variables.ContainsKey(\"{variableName}\")");
                    }
                }


                string variableEqualsNullPattern = @"[^ ]* == null";
                foreach (Match match in Regex.Matches(condition, variableEqualsNullPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            variableName = variableName.Replace("(", "").Replace(")", "");
                            condition = condition.Replace(variableName, $"!context.Variables.ContainsKey(\"{variableName.Replace("== null", "").Trim()}\")");
                        }
                    }
                }

                string variableDoesNotEqualsNullPattern = @"[^ ]* != null";
                foreach (Match match in Regex.Matches(condition, variableDoesNotEqualsNullPattern))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            variableName = variableName.Replace("(", "").Replace(")", "");
                            condition = condition.Replace(variableName, $"context.Variables.ContainsKey(\"{variableName.Replace("!= null", "").Trim()}\")");
                        }
                    }
                }

                string variableEqualsValue = @"[^ ]* (=|==|!=) [^ ]*";
                foreach (Match match in Regex.Matches(condition, variableEqualsValue))
                {
                    if (match.Success && match.Groups.Count > 0)
                    {
                        var variableName = match.Groups[0].Value;
                        var conditionOperator = match.Groups[1].Value;
                        var variableAndValue = variableName.Replace("(", "").Replace(")", "").Split(conditionOperator.Trim());
                        if (!variableName.Contains("context") && !string.IsNullOrEmpty(variableName))
                        {
                            var variableValue = variableAndValue[1].Trim();
                            var apimVariableName = variableAndValue[0].Trim();
                            conditionOperator = conditionOperator.Trim().StartsWith("=") ? "==" : "!=";
                            condition = condition.Replace(variableName, $"context.Variables.GetValueOrDefault<{GetDataTypeFromStringValue(variableValue)}>(\"{apimVariableName}\") {conditionOperator} {variableValue}");
                        }
                    }
                }



                condition = WebUtility.HtmlDecode($"@({condition})");
                var conditionelement = new XElement("choose", new XElement("when", ""));
                conditionelement.Element("when").SetAttributeValue("condition", condition);
                conditionelement.Element("when").Add(policy);
                return conditionelement;
            }
            else
                return policy;
        }

        private string GetDataTypeFromStringValue(string str)
        {
            bool boolValue;
            Int32 intValue;
            Int64 bigintValue;
            double doubleValue;
            DateTime dateValue;

            if (bool.TryParse(str, out boolValue))
                return "bool";
            else if (Int32.TryParse(str, out intValue))
                return "int";
            else if (Int64.TryParse(str, out bigintValue))
                return "Int64";
            else if (double.TryParse(str, out doubleValue))
                return "double";
            else if (DateTime.TryParse(str, out dateValue))
                return "DateTime";
            else return "string";

        }


    }
}
