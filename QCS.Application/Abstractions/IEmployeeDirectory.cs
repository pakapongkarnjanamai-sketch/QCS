namespace QCS.Application.Abstractions
{
    public sealed record EmployeeOrgDetails(
        string NId,
        string EmployeeName,
        string DepartmentCode,
        string DepartmentName
    );

    public interface IEmployeeDirectory
    {
        Task<EmployeeOrgDetails?> GetEmployeeDetailsAsync(string nId, CancellationToken cancellationToken = default);
    }
}
