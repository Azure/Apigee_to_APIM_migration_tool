using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement apigeePolicyElement, string apigeePolicyName);
    }
}
