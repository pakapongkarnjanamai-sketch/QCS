using System.Globalization;

namespace QCS.Application.Abstractions
{
    /// <summary>
    /// Date/time abstraction for testability.
    /// Owned by the Application layer; implemented in Infrastructure.
    /// </summary>
    public interface IDateTime
    {
        DateTime Now { get; }
        CultureInfo CultureInfo { get; }
        DateTime UnixTime { get; }
    }
}
