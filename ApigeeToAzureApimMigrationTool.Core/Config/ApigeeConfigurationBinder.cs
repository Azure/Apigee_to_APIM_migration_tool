using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class ApigeeConfigurationBinder : BinderBase<ApigeeConfiguration>
    {
        private readonly Option<string> _organizationNameOption;
        private readonly Option<string> _authenticationBaseUrlOption;
        private readonly Option<string> _managementBaseUrlOption;
        private readonly Option<string> _passcodeOption;
        private readonly Option<string> _usernameOption;
        private readonly Option<string> _passwordOption;
        private readonly Option<string> _proxyOrProductOption;
        private readonly Option<string> _proxyOrProductNameOption;
        private readonly Option<string> _environmentNameOption;
        private readonly Option<string> _configDirOption;
        private readonly Option<bool> _useApigeeDisplayName;

        public ApigeeConfigurationBinder(
            Option<string> organizationNameOption,
            Option<string> authenticationBaseUrlOption,
            Option<string> managementBaseUrlOption,
            Option<string> passcodeOption,
            Option<string> usernameOption,
            Option<string> passwordOption,
            Option<string> proxyOrProductOption,
            Option<string> proxyOrProductNameOption,
            Option<string> environmentNameOption,
            Option<string> configDirOption,
            Option<bool> useApigeeDisplayName)
        {
            _organizationNameOption = organizationNameOption;
            _authenticationBaseUrlOption = authenticationBaseUrlOption;
            _managementBaseUrlOption = managementBaseUrlOption;
            _passcodeOption = passcodeOption;
            _usernameOption = usernameOption;
            _passwordOption = passwordOption;
            _proxyOrProductOption = proxyOrProductOption;
            _proxyOrProductNameOption = proxyOrProductNameOption;
            _environmentNameOption = environmentNameOption;
            _configDirOption = configDirOption;
            _useApigeeDisplayName = useApigeeDisplayName;
        }

        protected override ApigeeConfiguration GetBoundValue(BindingContext bindingContext) =>
            new ApigeeConfiguration
            {
                OrganizationName = bindingContext.ParseResult.GetValueForOption(_organizationNameOption),
                AuthenticationBaseUrl = bindingContext.ParseResult.GetValueForOption(_authenticationBaseUrlOption),
                ManagementBaseUrl = bindingContext.ParseResult.GetValueForOption(_managementBaseUrlOption),
                Passcode = bindingContext.ParseResult.GetValueForOption(_passcodeOption),
                Username = bindingContext.ParseResult.GetValueForOption(_usernameOption),
                Password = bindingContext.ParseResult.GetValueForOption(_passwordOption),
                ProxyOrProduct = bindingContext.ParseResult.GetValueForOption(_proxyOrProductOption),
                ProxyOrProductName = bindingContext.ParseResult.GetValueForOption(_proxyOrProductNameOption),
                EnvironmentName = bindingContext.ParseResult.GetValueForOption(_environmentNameOption),
                ConfigDir = bindingContext.ParseResult.GetValueForOption(_configDirOption),
                UseApigeeDisplayName = bindingContext.ParseResult.GetValueForOption(_useApigeeDisplayName)
            };
    }
}
