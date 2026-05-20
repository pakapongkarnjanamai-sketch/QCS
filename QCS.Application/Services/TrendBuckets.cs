using System.Globalization;

namespace QCS.Application.Services
{
    /// <summary>
    /// Helper สร้างช่วงเวลา (buckets) สำหรับกราฟแบบ daily/weekly/monthly
    /// ใช้ตรรกะเดียวกับ DashboardController.BuildBucketsForTrend
    /// </summary>
    public static class TrendBuckets
    {
        public record Bucket(DateTime Start, DateTime End, string Label);

        public static List<Bucket> Build(string? timeframe, string? aggregation)
        {
            var today = DateTime.Today;
            var tf = (timeframe ?? "7d").Trim().ToLowerInvariant();
            var agg = (aggregation ?? "day").Trim().ToLowerInvariant();

            var rangeStart = tf switch
            {
                "30d" => today.AddDays(-29),
                "6m" => today.AddMonths(-6).AddDays(1),
                "1y" => today.AddYears(-1).AddDays(1),
                _ => today.AddDays(-6),
            };

            var buckets = new List<Bucket>();

            if (agg == "month")
            {
                var s = new DateTime(rangeStart.Year, rangeStart.Month, 1);
                while (s <= today)
                {
                    var e = s.AddMonths(1);
                    buckets.Add(new Bucket(s, e, s.ToString("MMM yy", CultureInfo.InvariantCulture)));
                    s = e;
                }
            }
            else if (agg == "week")
            {
                var dow = (int)rangeStart.DayOfWeek;
                var daysToMonday = dow == 0 ? 6 : dow - 1;
                var weekStart = rangeStart.AddDays(-daysToMonday);
                while (weekStart <= today)
                {
                    var weekEnd = weekStart.AddDays(7);
                    var iso = ISOWeek.GetWeekOfYear(weekStart);
                    buckets.Add(new Bucket(weekStart, weekEnd, $"W{iso:D2}"));
                    weekStart = weekEnd;
                }
            }
            else
            {
                var s = rangeStart;
                while (s <= today)
                {
                    buckets.Add(new Bucket(s, s.AddDays(1), s.ToString("d MMM", CultureInfo.InvariantCulture)));
                    s = s.AddDays(1);
                }
            }

            return buckets;
        }
    }
}
