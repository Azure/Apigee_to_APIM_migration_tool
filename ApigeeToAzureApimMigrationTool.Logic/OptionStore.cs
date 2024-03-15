using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{

    public class OptionStore
    {
        private readonly Dictionary<string, string> _options;
        public OptionStore()
        {
            _options = new Dictionary<string, string>();
        }

        public void Add(string key, string value)
        {
            _options.Add(key, value);
        }

        public string Get(string key)
        {
            return _options[key];
        }

        public bool Contains(string key)
        {
            return _options.ContainsKey(key);
        }

    }
}
