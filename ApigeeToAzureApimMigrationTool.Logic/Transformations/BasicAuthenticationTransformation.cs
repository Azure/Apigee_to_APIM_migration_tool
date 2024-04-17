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

            var username = element.Element("User")?.Attribute("ref")?.Value;
            var password = element.Element("Password")?.Attribute("ref")?.Value;

            string usernameValue;
            string passwordValue;

            var operation = element.Element("Operation")?.Value;
            if (operation.Equals("Decode", StringComparison.InvariantCultureIgnoreCase))
            {
                var source = element.Element("Source").Value;
                string sourceValue=null;
                if (source.StartsWith("request.header."))
                {
                    sourceValue = $"context.Request.Headers.GetValueOrDefault(\"{source.Replace("request.header.", "")}\")";
                }

                var userNameVariablePolicy = new XElement("set-variable");
                userNameVariablePolicy.Add(new XAttribute("name", username));
                userNameVariablePolicy.Add(new XAttribute("value", $@"@{{
                    Encoding encoding = Encoding.GetEncoding(""iso-8859-1"");
                    string usernamePassword = encoding.GetString(Convert.FromBase64String({sourceValue}));
                    int seperatorIndex = usernamePassword.IndexOf(':');
                    return usernamePassword.Substring(0, seperatorIndex);
                    }}"));

                policyList.Add(userNameVariablePolicy);

                var PasswordVariablePolicy = new XElement("set-variable");
                PasswordVariablePolicy.Add(new XAttribute("name", password));
                PasswordVariablePolicy.Add(new XAttribute("value", $@"@{{
                    Encoding encoding = Encoding.GetEncoding(""iso-8859-1"");
                    string usernamePassword = encoding.GetString(Convert.FromBase64String({sourceValue}));
                    int seperatorIndex = usernamePassword.IndexOf(':');
                    return usernamePassword.Substring(seperatorIndex + 1);
                    }}"));

                policyList.Add(PasswordVariablePolicy);

            }
            else
            {
                if (username.StartsWith("request.header"))
                    usernameValue = $"@(context.Request.Headers.GetValueOrDefault(\"{username.Replace("request.header.", "")}\"))";
                else if (username.StartsWith("request.queryparam"))
                    usernameValue = $"@(context.Url.Query.GetValueOrDefault(\"{username.Replace("request.queryparam.", "")}\"))";
                else
                    usernameValue = $"@(context.Variables.GetValueOrDefault<string>(\"{username}\",\"\"))";

                if (password.StartsWith("request.header"))
                    passwordValue = $"@(context.Request.Headers.GetValueOrDefault(\"{password.Replace("request.header.", "")}\"))";
                else if (password.StartsWith("request.queryparam"))
                    passwordValue = $"@(context.Url.Query.GetValueOrDefault(\"{password.Replace("request.queryparam.", "")}\"))";
                else
                    passwordValue = $"@(context.Variables.GetValueOrDefault<string>(\"{password}\",\"\"))";

                basicAuthenticationPolicy.Add(new XAttribute("username", usernameValue), new XAttribute("password", passwordValue));

                policyList.Add(basicAuthenticationPolicy);
            }

            return Task.FromResult<IEnumerable<XElement>>(policyList);
        }
    }
}
