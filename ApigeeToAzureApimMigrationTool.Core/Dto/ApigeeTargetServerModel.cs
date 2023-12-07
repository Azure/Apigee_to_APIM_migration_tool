using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Dto
{
    public class ApigeeTargetServerModel
    {
        public string Host { get; set; }
        public bool IsEnabled { get; set; }
        public string Name { get; set; }
        public int Port { get; set; }
        public ApigeeTargetServerSSLInfo SSLInfo { get; set; }
    }

    public class ApigeeTargetServerSSLInfo
    {
        public ICollection<string> Ciphers { get; set; }
        public bool ClientAuthEnabled { get; set; }
        public bool Enabled { get; set; }
        public bool IgnoreValidationError { get; set; }
        public ICollection<string> Protocols { get; set; }
        public string TrustStore { get; set; }
    }
}
