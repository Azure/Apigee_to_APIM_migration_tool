using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Core.Config
{
    public class ApimConfigurationBinder : BinderBase<ApimConfiguration>
    {
        private readonly Option<string> _urlOption;
        private readonly Option<string> _nameOption;
        private readonly Option<string> _resourceGroupOption;
        private readonly Option<string> _oAuthConfigNameOption;
        private readonly Option<string> _oAuthBackendAppIdOption;
        private readonly Option<string> _oAuthTenantIdOption;
        private readonly Option<string> _oAuthAudienceOption;
        private readonly Option<string> _oAuthIssuersOptionn;
        private readonly Option<string> _oAuthScopeClaimNameOptionn;

        public ApimConfigurationBinder(
            Option<string> urlOption,
            Option<string> nameOption,
            Option<string> resourceGroupOption,
            Option<string> oAuthConfigNameOption,
            Option<string> oAuthBackendAppIdOption,
            Option<string> oAuthTenantIdOption,
            Option<string> oAuthAudienceOption,
            Option<string> oAuthIssuersOptionn,
            Option<string> oAuthScopeClaimNameOptionn)
        {
            _nameOption = nameOption;
            _resourceGroupOption = resourceGroupOption;
            _oAuthConfigNameOption = oAuthConfigNameOption;
            _oAuthBackendAppIdOption = oAuthBackendAppIdOption;
            _oAuthTenantIdOption = oAuthTenantIdOption;
            _oAuthAudienceOption = oAuthAudienceOption;
            _oAuthIssuersOptionn = oAuthIssuersOptionn;
            _oAuthScopeClaimNameOptionn = oAuthScopeClaimNameOptionn;
        }

        protected override ApimConfiguration GetBoundValue(BindingContext bindingContext) => new ApimConfiguration
        {
            Url = bindingContext.ParseResult.GetValueForOption(_urlOption),
            Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
            ResourceGroup = bindingContext.ParseResult.GetValueForOption(_resourceGroupOption),
            OAuthConfigName = bindingContext.ParseResult.GetValueForOption(_oAuthConfigNameOption),
            OAuthBackendAppId = bindingContext.ParseResult.GetValueForOption(_oAuthBackendAppIdOption),
            OAuthTenantId = bindingContext.ParseResult.GetValueForOption(_oAuthTenantIdOption),
            OAuthAudiences = bindingContext.ParseResult.GetValueForOption(_oAuthAudienceOption),
            OAuthIssuers = bindingContext.ParseResult.GetValueForOption(_oAuthIssuersOptionn),
            OAuthScopeClaimName = bindingContext.ParseResult.GetValueForOption(_oAuthScopeClaimNameOptionn)
        };

    }
}
