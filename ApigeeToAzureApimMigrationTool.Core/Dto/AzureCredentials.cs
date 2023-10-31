using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Dto
{
    public class AzureCredentials
    {
        public Guid AppId { get; set; }
        public string DisplayName { get; set; }
        public string Password { get; set; }
        public Guid Tenant { get; set; }
        public string SubscriptionId { get; set; }
    }
}
