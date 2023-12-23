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
        public ExtractVariablesTransformation(IList<KeyValuePair<string, string>> policyVariables)
        {
            _policyVariables = policyVariables;
        }   
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            var apimPolicyElements = new List<XElement>();
            if (element.Element("JSONPayload") != null)
            {
                apimPolicyElements.Add(ExtractJsonValue(element, apigeePolicyName));
            }

            return Task.FromResult(apimPolicyElements.AsEnumerable());

        }

        private XElement ExtractJsonValue(XElement element, string policyName)
        {
            var jsonVariableName = element.Element("Source").Value;
            var variablePrefix = element.Element("VariablePrefix").Value;
            var variableName = element.Element("JSONPayload").Element("Variable").Attribute("name").Value;
            var jsonPath = element.Element("JSONPayload").Element("Variable").Element("JSONPath").Value.Replace("$", "");
            //                ((Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(context.Variables.GetValueOrDefault<string>(\" + jsonVariableName + "\"))){jsonPath})
            string apimExpression = "@{" +
                    "JObject json = JObject.Parse(context.Variables.GetValueOrDefault<string>(\"" + jsonVariableName + "\"));" +
                    "var typeCode = json.GetValue(\"" + jsonPath.Replace(".", "").Trim() + "\");" +
                    "return typeCode;" +
                    "}";

            var newPolicy = new XElement("set-variable", new XAttribute("name", $"{variablePrefix}.{variableName}"), new XAttribute("value", apimExpression));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return newPolicy;
        }

    }
}
