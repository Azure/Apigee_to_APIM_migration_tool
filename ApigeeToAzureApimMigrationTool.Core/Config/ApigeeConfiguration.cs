using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class ApigeeConfiguration
    {
        public string? OrganizationName { get; set; }
        public string? AuthenticationBaseUrl { get; set; }
        public string? ManagementBaseUrl { get; set; }
        public string? Passcode { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ProxyOrProduct { get; set; }
        public string? ProxyOrProductName { get; set; }
        public string? EnvironmentName { get; set; }
        public string? ConfigDir { get; set; }
        public bool UseTestService { get; set; }
        public bool UseApigeeDisplayName { get; set; }
    }
}
