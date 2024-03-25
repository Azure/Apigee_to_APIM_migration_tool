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
    public class BasicAuthenticationTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var policyList = new List<XElement>();
            var basicAuthenticationPolicy = new XElement("authentication-basic");

            var operation = element.Element("Operation")?.Value;
            if (operation.Equals("Decode", StringComparison.InvariantCultureIgnoreCase))
                throw new Exception("Basic Authentication Decode operation is not supported");

            var username = element.Element("User")?.Attribute("ref")?.Value;
            var password = element.Element("Password")?.Attribute("ref")?.Value;

            string usernameValue;
            string passwordValue;

            if (username.StartsWith("request.header"))
                usernameValue = $"@(context.Headers.GetValueOrDefault(\"{username.Replace("request.header.", "")}\"))";
            else if (username.StartsWith("request.queryparam"))
                usernameValue = $"@(context.Url.Query.GetValueOrDefault(\"{username.Replace("request.queryparam.", "")}\"))";
            else
                usernameValue = $"@(context.Variables.GetValueOrDefault<string>(\"{username}\",\"\"))";

            if (password.StartsWith("request.header"))
                passwordValue = $"@(context.Headers.GetValueOrDefault(\"{password.Replace("request.header.", "")}\"))";
            else if (password.StartsWith("request.queryparam"))
                passwordValue = $"@(context.Url.Query.GetValueOrDefault(\"{password.Replace("request.queryparam.", "")}\"))";
            else
                passwordValue = $"@(context.Variables.GetValueOrDefault<string>(\"{password}\",\"\"))";

            basicAuthenticationPolicy.Add(new XAttribute("username", usernameValue), new XAttribute("password", passwordValue));

            policyList.Add(basicAuthenticationPolicy);
            return Task.FromResult<IEnumerable<XElement>>(policyList);
        }
    }
}
