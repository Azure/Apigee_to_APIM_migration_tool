using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class EntraConfigurationBinder : BinderBase<EntraConfiguration>
    {
        private readonly Option<string> _appIdOption;
        private readonly Option<string> _passwordOption;
        private readonly Option<string> _tenantIdOption;
        private readonly Option<string> _subscriptionIdOption;

        public EntraConfigurationBinder(
            Option<string> appIdOption,
            Option<string> passwordOption,
            Option<string> tenantIdOption,
            Option<string> subscriptionIdOption)
        {
            _appIdOption = appIdOption;
            _passwordOption = passwordOption;
            _tenantIdOption = tenantIdOption;
            _subscriptionIdOption = subscriptionIdOption;
        }

        protected override EntraConfiguration GetBoundValue(BindingContext bindingContext) =>
            new EntraConfiguration
            {
                AppId = bindingContext.ParseResult.GetValueForOption(_appIdOption),
                Password = bindingContext.ParseResult.GetValueForOption(_passwordOption),
                TenantId = bindingContext.ParseResult.GetValueForOption(_tenantIdOption),
                SubscriptionId = bindingContext.ParseResult.GetValueForOption(_subscriptionIdOption)
            };
    }
}
