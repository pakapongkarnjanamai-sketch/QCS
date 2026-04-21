using System.Globalization;
using QCS.Application.Abstractions;

namespace QCS.Infrastructure.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IDateTime"/> that returns Bangkok time (UTC+7).
    /// </summary>
    public class DateTimeService : IDateTime
    {
        public DateTime Now => DateTime.UtcNow.AddHours(7);
        public CultureInfo CultureInfo => new("th-TH");
        public DateTime UnixTime => new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
