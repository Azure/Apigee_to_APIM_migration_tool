using ApigeeToApimMigrationTool.Core.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IApimPolicyTransformer
    {
        Task TransformPoliciesInCollection(IEnumerable<XElement>? elements, XElement azureApimPolicySection, Func<string, string, XDocument> xmlLoader,
            string apimName, string proxyName, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig);
    }
}
