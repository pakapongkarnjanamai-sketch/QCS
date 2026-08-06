using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Routing;
using QCS.API.Controllers;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    /// <summary>
    /// Proves the MVC portal stayed removed (PLAN-051 Phase 6).
    ///
    /// A decommission is not finished when the files are deleted; it is finished when nothing can
    /// quietly bring them back. Nothing else in this suite would notice a restored project, a
    /// re-added PortalCutover switch, or a resurrected write route — the build would simply pass.
    /// These read the repository and the compiled controllers rather than any running server.
    /// </summary>
    public class DecommissionAssertionTests
    {
        /// <summary>
        /// Walks up from the test assembly to the directory holding QCS.sln. Test binaries live
        /// several levels deep and the depth changes with --artifacts-path, so the marker file is
        /// searched for rather than counted to.
        /// </summary>
        private static DirectoryInfo RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QCS.sln")))
            {
                directory = directory.Parent;
            }

            directory.ShouldNotBeNull("Could not locate QCS.sln above the test output directory.");
            return directory!;
        }

        [Fact]
        public void The_MVC_portal_project_is_absent_from_the_solution_and_the_working_tree()
        {
            var root = RepositoryRoot();

            Directory.Exists(Path.Combine(root.FullName, "QCS.Web.User"))
                .ShouldBeFalse("QCS.Web.User is back on disk. It was removed in PLAN-051 Phase 6 and React User is the only user portal.");

            File.ReadAllText(Path.Combine(root.FullName, "QCS.sln"))
                .ShouldNotContain("QCS.Web.User", Case.Insensitive,
                    customMessage: "QCS.Web.User is referenced by QCS.sln again.");
        }

        /// <summary>
        /// Path of this source file. A helper rather than a parameter default because xUnit does
        /// not allow parameters on a [Fact].
        /// </summary>
        private static string ThisFile([CallerFilePath] string path = "") => path;

        [Fact]
        public void No_PortalCutover_switch_exists_anywhere_in_the_source()
        {
            var thisFile = ThisFile();
            // The cutover redirector was the toggle that could send users back to MVC. There is no
            // MVC to go back to, so a reference to it now means someone is rebuilding a bridge to
            // a portal that no longer exists.
            //
            // This file is excluded because it contains the search term itself — the first run
            // failed by finding its own assertion. CallerFilePath rather than a hardcoded name so
            // renaming this file does not silently turn the check off.
            var root = RepositoryRoot();
            var offenders = SourceFiles(root)
                .Where(file => !string.Equals(file, thisFile, StringComparison.OrdinalIgnoreCase))
                .Where(file => File.ReadAllText(file).Contains("PortalCutover", StringComparison.Ordinal))
                .Select(file => Path.GetRelativePath(root.FullName, file))
                .ToList();

            offenders.ShouldBeEmpty($"PortalCutover is referenced again in: {string.Join(", ", offenders)}");
        }

        [Fact]
        public void The_legacy_user_mutation_routes_are_gone_from_RequestController()
        {
            // Save/Submit/Update/SubmitUpdate wrote request state through the retired local
            // workflow engine. Deleting the MVC views is not enough — while these routes exist,
            // anything holding a session can still drive the old lifecycle over HTTP.
            var retired = new[] { "Save", "Submit", "Update", "SubmitUpdate" };

            var actions = typeof(RequestController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToList();

            foreach (var name in retired)
            {
                actions.ShouldNotContain(name,
                    $"RequestController.{name} is back. Central approval owns the request lifecycle; " +
                    "a second write path into it will disagree with the Approval service.");
            }
        }

        [Fact]
        public void The_ApprovalController_write_surface_is_gone()
        {
            typeof(RequestController).Assembly
                .GetTypes()
                .Any(type => type.Name == "ApprovalController")
                .ShouldBeFalse("ApprovalController is back. Approve/Reject now belong to the central service via PortalRequestsController.");
        }

        /// <summary>
        /// Every retained /api/Request route must have a named consumer. The plan kept this
        /// controller only because React Admin uses parts of it; without a written-down list, the
        /// next reader cannot tell a live endpoint from one that was simply never deleted.
        ///
        /// This is a documentation test on purpose. It fails when someone adds a route without
        /// saying who calls it, which is the moment to decide rather than months later.
        /// </summary>
        [Fact]
        public void Every_retained_Request_route_has_a_named_consumer()
        {
            var consumers = new (string Action, string Consumer)[]
            {
                ("GetMyRequests", "unused — candidate for removal; only a doc-comment example in QCS.React.Admin/src/lib/createDataSource.ts"),
                ("GetMyTasks", "QCS.React.User workspace (MyTasks view)"),
                ("GetApprovedList", "QCS.React.User workspace"),
                ("GetMyApprovedList", "QCS.React.User workspace"),
                ("GetAllRequests", "QCS.React.Admin RequestsPage + OverviewPage (Admin/All)"),
                ("GetAllDraftRequests", "QCS.React.Admin RequestsPage + OverviewPage (Admin/Draft)"),
                ("GetAllPendingRequests", "QCS.React.Admin RequestsPage + OverviewPage (Admin/Pending)"),
                ("GetAllApprovedRequests", "QCS.React.Admin RequestsPage + OverviewPage + QuotationsPage (Admin/Approved)"),
                ("GetAllRejectedRequests", "QCS.React.Admin RequestsPage + OverviewPage (Admin/Rejected)"),
                ("GetRejectedRequests", "QCS.React.User workspace (Rejected view)"),
                ("GetAllApprovedRequestsByVendor", "QCS.React.Admin QuotationsPage vendor filter"),
                ("GetAllApprovedRequestsByRequesterNId", "QCS.React.Admin QuotationsPage requester filter"),
                ("GetAllApprovedRequestsByRequester", "QCS.React.Admin QuotationsPage requester-name filter"),
                ("GetApprovedRequesters", "QCS.React.Admin OverviewPage top-requesters list"),
                ("GetRequestDetail", "QCS.API internal + integration reads"),
                ("GetRequestDetailByCode", "QRS integration and QCS.React.Admin detail"),
                ("GetRequestDetailByCodeQuery", "QRS integration (query-string form)"),
                ("Delete", "QCS.React.Admin RequestsPage delete action"),
                ("ViewFile", "QCS.React.Admin QuotationsPage + QCS.React.User document preview"),
                ("PreviewMergeStamp", "QCS.React.Admin quotation preview"),
            };

            var declared = typeof(RequestController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(method => method.Name)
                .Distinct()
                .ToList();

            var undocumented = declared.Except(consumers.Select(entry => entry.Action)).ToList();
            undocumented.ShouldBeEmpty(
                $"These RequestController routes have no named consumer: {string.Join(", ", undocumented)}. " +
                "Add the caller to this list, or delete the route.");

            var stale = consumers.Select(entry => entry.Action).Except(declared).ToList();
            stale.ShouldBeEmpty(
                $"These entries name routes that no longer exist: {string.Join(", ", stale)}. Remove them from the list.");
        }

        private static IEnumerable<string> SourceFiles(DirectoryInfo root)
        {
            var extensions = new[] { ".cs", ".ts", ".tsx", ".json", ".ps1" };
            var skip = new[] { "\\obj\\", "\\bin\\", "\\node_modules\\", "\\.vs\\", "\\.git\\", "\\artifacts\\", "\\dist\\" };

            return Directory
                .EnumerateFiles(root.FullName, "*.*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file)))
                .Where(file => !skip.Any(fragment => file.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
