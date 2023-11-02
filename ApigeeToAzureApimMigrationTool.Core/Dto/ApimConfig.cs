using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.dto
{
    public class ApimConfig
    {
        public string ApimUrl { get; set; }
        public string ApimName { get; set; }
        public string ApimResourceGroupName { get; set; }
    }
}
