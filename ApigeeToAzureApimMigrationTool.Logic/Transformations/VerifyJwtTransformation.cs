﻿using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class VerifyJwtTransformation : IPolicyTransformation
    {
        private readonly IList<KeyValuePair<string, string>> _policyVariables;

        public VerifyJwtTransformation(IList<KeyValuePair<string, string>> policyVariables)
        {
            _policyVariables = policyVariables;
        }
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>
            {
                ValidateJwt(element)
            };

            return Task.FromResult(apimPolicies.AsEnumerable());
        }
        private XElement ValidateJwt(XElement element, string condition = null)
        {
            var token = element.Element("Source").Value;
            var key = element.Element("PublicKey").Element("Value").Attribute("ref").Value;

            var decryptionKeyelement = new XElement("decryption-keys");
            decryptionKeyelement.Add(new XElement("key", $"@(context.Variables.GetValueOrDefault<string>(\"{key}\"))"));
            string outputVariableName = $"{token}_validated";
            var newPolicy = new XElement("validate-jwt", new XAttribute("token-value", $"@(context.Variables.GetValueOrDefault<string>(\"{token}\"))"), decryptionKeyelement,
                new XAttribute("output-token-variable-name", outputVariableName));
            _policyVariables.Add(new KeyValuePair<string, string>(element.Attribute("name").Value, outputVariableName));
            return newPolicy;
        }

    }
}