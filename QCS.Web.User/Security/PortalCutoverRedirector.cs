using Microsoft.Extensions.Options;

namespace QCS.Web.User.Security
{
    /// <summary>
    /// The single place that knows how a legacy portal URL maps onto a React portal URL.
    ///
    /// Kept in one class rather than spread across three controllers so the route parity matrix in
    /// PLAN-040 has exactly one implementation to check against. Every method returns an absolute
    /// path, never a relative one, because the two portals are separate IIS applications.
    /// </summary>
    public sealed class PortalCutoverRedirector
    {
        private readonly IOptionsMonitor<PortalCutoverOptions> _options;
        private readonly ILogger<PortalCutoverRedirector> _logger;
        private int _warnedAboutBasePath;

        public PortalCutoverRedirector(
            IOptionsMonitor<PortalCutoverOptions> options,
            ILogger<PortalCutoverRedirector> logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// True only when cutover is switched on AND the target is somewhere this app is not.
        ///
        /// The second half matters: a BasePath that is blank, equal to this app's own root, or a
        /// path this app itself serves would make every request redirect to itself forever.
        /// Rather than trust configuration, refuse to redirect and say so once — a portal that
        /// keeps working while misconfigured beats one that loops.
        /// </summary>
        public bool IsEnabledFor(HttpRequest request)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled) return false;

            var basePath = options.NormalisedBasePath();
            var appRoot = request.PathBase.HasValue ? request.PathBase.Value!.TrimEnd('/') : string.Empty;

            if (basePath.Length == 0 || string.Equals(basePath, appRoot, StringComparison.OrdinalIgnoreCase))
            {
                WarnOnce(options.BasePath, appRoot, "it is empty or equal to this application's own root");
                return false;
            }

            // Second guard, and the one that catches a base path this application itself serves —
            // say BasePath were '/QCS/Home'. That is not equal to the app root, so the check above
            // lets it through, and every redirect would land back here. Checking the incoming path
            // instead of trusting configuration closes it: if the request we are handling already
            // lies under BasePath, the target is us, so do not redirect.
            var incoming = $"{appRoot}{request.Path.Value}";
            if (incoming.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(incoming.TrimEnd('/'), basePath, StringComparison.OrdinalIgnoreCase))
            {
                WarnOnce(options.BasePath, appRoot, "this application is serving requests underneath it");
                return false;
            }

            return true;
        }

        private void WarnOnce(string? configuredBasePath, string appRoot, string reason)
        {
            if (Interlocked.CompareExchange(ref _warnedAboutBasePath, 1, 0) != 0) return;

            _logger.LogWarning(
                "PortalCutover is enabled but BasePath ('{BasePath}') cannot be used because {Reason}. " +
                "This application's root is '{AppRoot}'. Redirects are suppressed to avoid a loop.",
                configuredBasePath,
                reason,
                appRoot);
        }

        private string Base() => _options.CurrentValue.NormalisedBasePath();

        /// <summary>
        /// The legacy landing page, which selects its list through a <c>view</c> query value.
        ///
        /// Three of those views have a dedicated SPA route and must go there directly rather than
        /// to the workspace carrying a query — going through the workspace would be a second hop,
        /// and PLAN-040 forbids redirect chains.
        /// </summary>
        public string Workspace(string? view) => (view ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "my-tasks" => $"{Base()}/inbox",
            "my-requests" => $"{Base()}/requests",
            "all-approved" => $"{Base()}/quotations",
            "my-approved" => $"{Base()}/?view=my-approved",
            "rejected" => $"{Base()}/?view=rejected",
            _ => $"{Base()}/",
        };

        public string Quotations() => $"{Base()}/quotations";

        public string QuotationDetail(string code) => $"{Base()}/quotations/{Uri.EscapeDataString(code)}";

        /// <summary>
        /// Legacy <c>/Request/Form/{id}</c> is the request's detail-and-actions page, not purely an
        /// editor, so a bookmark to it maps to the SPA's DETAIL route. Sending it to
        /// <c>/requests/{id}/edit</c> would drop a user into an edit form for a request they may
        /// have no right to edit.
        /// </summary>
        public string RequestDetail(int id) => $"{Base()}/requests/{id}";

        public string NewRequest() => $"{Base()}/requests/new";
    }
}
