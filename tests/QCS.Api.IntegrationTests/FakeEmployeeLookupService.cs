using QCS.API.Services;

namespace QCS.Api.IntegrationTests
{
    public sealed class FakeEmployeeLookupService : IEmployeeLookupService
    {
        public Task<IReadOnlyList<EmployeeDepartmentItem>> GetDepartmentItemsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EmployeeDepartmentItem>>(Array.Empty<EmployeeDepartmentItem>());

        public Task<Dictionary<string, string>> GetDepartmentMapByNIdsAsync(
            IEnumerable<string> nIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(nIds
                .Where(nId => !string.IsNullOrWhiteSpace(nId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(nId => nId, _ => "General Purchasing Division", StringComparer.OrdinalIgnoreCase));

        public Task<EmployeeFullItem?> GetEmployeeByNIdAsync(
            string nId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmployeeFullItem?>(new EmployeeFullItem
            {
                NId = nId,
                EnglishFirstName = "Test",
                EnglishLastName = nId,
                Division = "GPD",
                Department = "General Purchasing Division"
            });
    }
}