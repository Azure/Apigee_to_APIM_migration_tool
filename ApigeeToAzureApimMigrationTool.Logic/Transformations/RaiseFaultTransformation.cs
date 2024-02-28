using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class RaiseFaultTransformation : AssignMessageTransformation, IPolicyTransformation
    {
        public RaiseFaultTransformation(IExpressionTranslator expressionTranslator) : base(expressionTranslator)
        {
        }

        public Task<IEnumerable<XElement>> Transform(XElement apigeePolicyElement, string apigeePolicyName)
        {
            var policyList = new List<XElement>();
            var returnResponsePolicy = new XElement("return-response");
            var apimPolicies = BuildApimPolicyCollection(apigeePolicyElement.Element("FaultResponse")).ToList();
            string statusCode = "500";
            string reasonPhrase = "Server Error";

            string faultReason = string.Empty;
            if (apigeePolicyElement.Element("Set").Element("StatusCode") != null)
                statusCode = apigeePolicyElement.Element("Set").Element("StatusCode").Value;

            if (apigeePolicyElement.Element("Set").Element("ReasonPhrase") != null)
                reasonPhrase = apigeePolicyElement.Element("Set").Element("ReasonPhrase").Value;


            apimPolicies.Add(new XElement("set-status", new XAttribute("code", statusCode), new XAttribute("reason", reasonPhrase)));

            foreach(var policy in apimPolicies)
            {
                returnResponsePolicy.Add(policy);
            }

            policyList.Add(returnResponsePolicy);
            return Task.FromResult<IEnumerable<XElement>>(policyList);

        }
    }
}
