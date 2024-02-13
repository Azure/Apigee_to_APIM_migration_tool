using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class ApimConfiguration
    {
        public string? Url { get; set; }
        public string? Name { get; set; }
        public string? ResourceGroup { get; set; }
        public string? OAuthConfigName { get; set; }
        public string? OAuthBackendAppId { get; set; }
        public string? OAuthTenantId { get; set; }
    }
}
