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
        /// The second half matters: if BasePath were left blank, or pointed back at the legacy
        /// app's own root, every request would redirect to itself forever. Rather than trust
        /// configuration, refuse to redirect and say so once — a portal that keeps working while
        /// misconfigured beats one that loops.
        /// </summary>
        public bool IsEnabledFor(HttpRequest request)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled) return false;

            var basePath = options.NormalisedBasePath();
            var appRoot = request.PathBase.HasValue ? request.PathBase.Value!.TrimEnd('/') : string.Empty;

            var usable = basePath.Length > 0 &&
                !string.Equals(basePath, appRoot, StringComparison.OrdinalIgnoreCase);

            if (!usable && Interlocked.CompareExchange(ref _warnedAboutBasePath, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "PortalCutover is enabled but BasePath ('{BasePath}') is empty or equal to this " +
                    "application's own root ('{AppRoot}'). Redirects are suppressed to avoid a loop.",
                    options.BasePath,
                    appRoot);
            }

            return usable;
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
