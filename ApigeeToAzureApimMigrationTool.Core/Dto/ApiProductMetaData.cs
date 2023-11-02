using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.dto
{
    public class ApiProductMetaData
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public IList<string> Proxies { get; set; }
    }
}
