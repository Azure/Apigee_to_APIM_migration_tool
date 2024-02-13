using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class EntraConfiguration
    {
        public string? AppId { get; set; }
        public string? Password { get; set; }
        public string? TenantId { get; set; }
        public string? SubscriptionId { get; set; }
    }
}
