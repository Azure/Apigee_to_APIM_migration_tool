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
    public class AssignMessageTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement apigeePolicyElement, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>();

            if (apigeePolicyElement.Element("Add")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Add").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, remove: false));
                }
            }

            if (apigeePolicyElement.Element("Set")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Set").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, remove: false));
                }
            }


            if (apigeePolicyElement.Element("Remove")?.Element("Headers") != null)
            {
                foreach (var header in apigeePolicyElement.Element("Remove").Element("Headers").Elements("Header"))
                {
                    apimPolicies.Add(SetHeader(header, remove: true));
                }
            }

            // BUG?: Add doesn't support the Payload child element.  Only Set does.
            if (apigeePolicyElement.Element("Add")?.Element("Payload") != null)
            {
                apimPolicies.Add(SetBody(apigeePolicyElement.Element("Add").Element("Payload")));
            }

            if (apigeePolicyElement.Element("Set")?.Element("Payload") != null)
            {
                apimPolicies.Add(SetBody(apigeePolicyElement.Element("Set").Element("Payload")));
            }

            if (apigeePolicyElement.Element("AssignVariable") != null)
            {
                apimPolicies.Add(SetVariable(apigeePolicyElement));
            }

            return Task.FromResult(apimPolicies.AsEnumerable());

        }

        private XElement SetBody(XElement body)
        {
            var contentType = body.Attribute("contentType")?.Value;
            var value = body.Value;
            var newPolicy = new XElement("set-body");
            // TODO: Variable substitution can also be used in json payloads.
            if (!contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            {
                if (value.StartsWith('{'))
                {
                    value = value.Replace("{", "").Replace("}", "");
                    var expressionTranslator = new ExpressionTranslator();
                    value = expressionTranslator.Translate(value);
                    value = WebUtility.HtmlDecode($"@({value})");
                }
                newPolicy.Add(new XAttribute("template", "liquid"));
            }

            newPolicy.Value = value;
            return newPolicy;
        }

        private XElement SetHeader(XElement header, bool remove)
        {
            var name = header.Attribute("name")?.Value;
            var value = header.Value;
            if (value.StartsWith('{'))
            {
                value = value.Replace("{", "").Replace("}", "");
                var expressionTranslator = new ExpressionTranslator();
                value = expressionTranslator.Translate(value);
                value = WebUtility.HtmlDecode($"@({value})");
            }
            // BUG?: Per Apigee documentation, the Add element should not overwrite existing headers, and exists-action should
            // therefore be set to "skip".  Only Set should be set to "override."
            var newPolicy = new XElement("set-header", new XAttribute("name", name), new XAttribute("exists-action", remove ? "delete" : "override"));
            if (!remove)
                newPolicy.Add(new XElement("value", value));
            return newPolicy;
        }

        private XElement SetVariable(XElement assignMessageElement)
        {
            var assignVariable = assignMessageElement.Element("AssignVariable");
            var name = assignVariable.Element("Name")?.Value;
            var value = assignVariable.Element("Value")?.Value;
            var newPolicy = new XElement("set-variable", new XAttribute("name", name), new XAttribute("value", value));
            return newPolicy;
        }

    }
}
