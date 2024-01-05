using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IPolicyTransformationFactory
    {
        IPolicyTransformation GetTransformationForPolicy(string policyName, IList<KeyValuePair<string, string>> policyVariables);
    }
}
