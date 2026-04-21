namespace QCS.Application.Abstractions
{
    /// <summary>
    /// Provides information about the currently authenticated user.
    /// Owned by the Application layer; implemented in Infrastructure (HttpContext-based).
    /// </summary>
    public interface ICurrentUserService
    {
        string UserId { get; }
        string FullName { get; }
        bool IsAuthenticated { get; }
    }
}
