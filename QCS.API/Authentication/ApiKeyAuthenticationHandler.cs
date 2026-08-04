using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace QCS.API.Authentication
{
    public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "IntegrationApiKey";
        public const string HeaderName = "X-Api-Key";

        private readonly IOptionsMonitor<ApiKeyOptions> _apiKeyOptions;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptionsMonitor<ApiKeyOptions> apiKeyOptions)
            : base(options, logger, encoder)
        {
            _apiKeyOptions = apiKeyOptions;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var providedKey = values.ToString();
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!IsKnownKey(providedKey, _apiKeyOptions.CurrentValue.ApiKeys))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "integration-client")], Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new { error = "Missing or invalid API key." });
        }

        private static bool IsKnownKey(string providedKey, IReadOnlyList<string> configuredKeys)
        {
            var providedBytes = Encoding.UTF8.GetBytes(providedKey);
            var isMatch = false;

            foreach (var configuredKey in configuredKeys)
            {
                isMatch |= CryptographicOperations.FixedTimeEquals(
                    providedBytes,
                    Encoding.UTF8.GetBytes(configuredKey));
            }

            return isMatch;
        }
    }
}