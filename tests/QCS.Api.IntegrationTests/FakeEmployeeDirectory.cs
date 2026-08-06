using QCS.Application.Abstractions;

namespace QCS.Api.IntegrationTests
{
    public sealed class FakeEmployeeDirectory : IEmployeeDirectory
    {
        public Task<EmployeeOrgDetails?> GetEmployeeDetailsAsync(
            string nId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmployeeOrgDetails?>(new EmployeeOrgDetails(
                nId,
                $"Test {nId}",
                "GPD",
                "General Purchasing Division"));
    }
}