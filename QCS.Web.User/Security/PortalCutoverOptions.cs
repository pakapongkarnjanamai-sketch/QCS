namespace QCS.Web.User.Security
{
    /// <summary>
    /// Controls whether the legacy MVC portal hands its users to the React portal.
    ///
    /// Ships DISABLED. Deploying the redirect code and switching traffic are deliberately two
    /// separate acts: the switch is a config change on the server, and undoing that change is the
    /// rollback — no redeploy, no code revert. See PLAN-040 in the QRS repo.
    /// </summary>
    public sealed class PortalCutoverOptions
    {
        public const string SectionName = "PortalCutover";

        /// <summary>False in the repo on purpose. Only a server's own appsettings turns this on.</summary>
        public bool Enabled { get; set; }

        /// <summary>Where the React portal is mounted, e.g. <c>/QCS/User</c>.</summary>
        public string BasePath { get; set; } = "/QCS/User";

        /// <summary>BasePath with surrounding slashes normalised, or empty when it is unusable.</summary>
        public string NormalisedBasePath()
        {
            var trimmed = (BasePath ?? string.Empty).Trim().Trim('/');
            return trimmed.Length == 0 ? string.Empty : "/" + trimmed;
        }
    }
}
