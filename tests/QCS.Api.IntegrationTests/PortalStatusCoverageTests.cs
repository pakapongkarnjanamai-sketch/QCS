using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QCS.Domain.DTOs.Portal;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    /// <summary>
    /// Every one of the seven central statuses, end to end.
    ///
    /// The shared fixture only ever seeds Draft, InProcess, Completed and Rejected — the four the
    /// old local engine had. Returned, WaitingEffective and Cancelled arrived with the central
    /// contract and nothing exercised them, which is exactly how a status reaches a user as a blank
    /// badge or an exception months later. These seed their own rows so the shared fixture and its
    /// sixteen assertions are left alone.
    /// </summary>
    public class PortalStatusCoverageTests : IClassFixture<QcsApiFactory>
    {
        private const string Owner = "STATUSOWNER";
        private readonly QcsApiFactory _factory;

        public PortalStatusCoverageTests(QcsApiFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientAs(string nid)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"NIKONOA\\{nid}");
            return client;
        }

        // Ids are offset well clear of the shared fixture's rows; the two share one in-memory
        // database and a collision would surface as an unrelated test failing.
        private static int IdFor(RequestStatus status) => 7100 + (int)status;

        private void Seed(RequestStatus status)
        {
            _factory.SeedDatabase(db =>
            {
                var id = IdFor(status);
                if (db.Requests.Any(r => r.Id == id)) return;

                db.Requests.Add(new Request
                {
                    Id = id,
                    Code = $"QC-20260806-{id}",
                    Title = $"Status coverage {status}",
                    VendorCode = "V900",
                    VendorName = "Status Vendor",
                    RequestDate = new DateTime(2026, 8, 6, 9, 0, 0),
                    Status = (int)status,
                    // Null on purpose for the terminal states: after PLAN-051 the current step is
                    // nullable and no longer carries a 99/-1 sentinel. A test that always sets a
                    // number would not notice the nullability regressing.
                    CurrentStepSequence = status is RequestStatus.Draft or RequestStatus.InProcess or RequestStatus.Returned ? 1 : null,
                    CreatedBy = Owner,
                    IsActive = true,
                });
            });
        }

        [Theory]
        [InlineData(RequestStatus.Draft, "Draft")]
        [InlineData(RequestStatus.InProcess, "InProcess")]
        [InlineData(RequestStatus.Returned, "Returned")]
        [InlineData(RequestStatus.Rejected, "Rejected")]
        [InlineData(RequestStatus.WaitingEffective, "WaitingEffective")]
        [InlineData(RequestStatus.Completed, "Completed")]
        [InlineData(RequestStatus.Cancelled, "Cancelled")]
        public async Task Detail_renders_every_central_status_by_its_exact_name(RequestStatus status, string expectedName)
        {
            Seed(status);

            var response = await CreateClientAs(Owner).GetAsync($"/api/Portal/Requests/{IdFor(status)}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{status} should be readable by its creator.");

            var detail = await response.Content.ReadFromJsonAsync<PortalRequestDetailDto>();
            detail.ShouldNotBeNull();
            detail!.Status.ShouldBe((int)status);

            // The exact name matters: both portals key their badge off this string, and QRS mirrors
            // the numeric value. A renamed enum member is a contract change, not a cosmetic one.
            detail.StatusName.ShouldBe(expectedName);
        }

        [Theory]
        [InlineData(RequestStatus.Rejected)]
        [InlineData(RequestStatus.WaitingEffective)]
        [InlineData(RequestStatus.Completed)]
        [InlineData(RequestStatus.Cancelled)]
        public async Task A_terminal_request_is_not_editable_or_deletable(RequestStatus status)
        {
            Seed(status);

            var detail = await CreateClientAs(Owner)
                .GetFromJsonAsync<PortalRequestDetailDto>($"/api/Portal/Requests/{IdFor(status)}");

            detail.ShouldNotBeNull();

            // CanEdit/CanDelete stay local and mean "a local Draft owned by me". Terminal states
            // must not offer them even to the creator — the document is closed centrally, and a
            // local edit would put the mirror ahead of the authority.
            detail!.Permissions.CanEdit.ShouldBeFalse($"{status} must not be editable.");
            detail.Permissions.CanDelete.ShouldBeFalse($"{status} must not be deletable.");
        }

        [Fact]
        public async Task A_draft_is_editable_by_its_creator_and_carries_no_current_step_sentinel()
        {
            Seed(RequestStatus.Draft);

            var detail = await CreateClientAs(Owner)
                .GetFromJsonAsync<PortalRequestDetailDto>($"/api/Portal/Requests/{IdFor(RequestStatus.Draft)}");

            detail.ShouldNotBeNull();
            detail!.Permissions.CanEdit.ShouldBeTrue();
            detail.Permissions.CanDelete.ShouldBeTrue();
            detail.CurrentStepSequence.ShouldNotBe(99, "99 was the retired local engine's terminal sentinel.");
            detail.CurrentStepSequence.ShouldNotBe(-1, "-1 was the retired local engine's rejected sentinel.");
        }

        [Fact]
        public async Task A_terminal_request_reports_no_current_step()
        {
            Seed(RequestStatus.Completed);

            var detail = await CreateClientAs(Owner)
                .GetFromJsonAsync<PortalRequestDetailDto>($"/api/Portal/Requests/{IdFor(RequestStatus.Completed)}");

            detail.ShouldNotBeNull();
            // Null, not 99. The column was made nullable precisely so "finished" stops being
            // encoded as a magic number that every caller has to know about.
            detail!.CurrentStepSequence.ShouldBeNull();
        }
    }
}
