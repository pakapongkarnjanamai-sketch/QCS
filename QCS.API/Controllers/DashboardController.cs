using Microsoft.AspNetCore.Authorization;
using QCS.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using System;
using System.Globalization;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        // ✅ เปลี่ยน Type เป็น Interface
        private readonly IRequestService _requestService;

        // ✅ เปลี่ยน Parameter ใน Constructor เป็น Interface
        public DashboardController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpGet("Summary")]
        public async Task<ActionResult<DashboardDto>> GetSummary()
        {
            try
            {
                // NOTE: These queries must run sequentially because they share the same DbContext.
                var myTaskCount = await _requestService.GetMyPendingTaskCountAsync();
                var myApprovedCount = await _requestService.GetMyApprovedListQuery().CountAsync();
                var myRejectedCount = await _requestService.GetRejectedRequestsQuery().CountAsync();
                var myRequestCount = await _requestService.GetMyRequestsQuery().CountAsync();

                return Ok(new DashboardDto
                {
                    MyTaskCount = myTaskCount,
                    MyApprovedCount = myApprovedCount,
                    MyRejectedCount = myRejectedCount,
                    MyRequestCount = myRequestCount,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("RequestTrend")]
        public async Task<IActionResult> GetRequestTrend(
            [FromQuery] string timeframe = "7d",
            [FromQuery] string aggregation = "day",
            [FromQuery] string? granularity = null)
        {
            // Backward-compat: map legacy granularity param to new params
            if (granularity != null)
            {
                (timeframe, aggregation) = (granularity.Trim().ToLowerInvariant()) switch
                {
                    "year" => ("1y", "month"),
                    "month" => ("30d", "week"),
                    _ => ("7d", "day"),
                };
            }

            try
            {
                var buckets = BuildBucketsForTrend(timeframe, aggregation);
                var startOfRange = buckets[0].Start;

                var rows = await _requestService.GetAllRequestsQuery()
                    .Where(r => r.RequestDate >= startOfRange)
                    .Select(r => new { r.RequestDate })
                    .ToListAsync();

                var result = buckets.Select(b => new RequestTrendPointDto
                {
                    Year = b.Start.Year,
                    Month = b.Start.Month,
                    Label = b.Label,
                    Count = rows.Count(r => r.RequestDate >= b.Start && r.RequestDate < b.End),
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("RequesterTrend")]
        public async Task<IActionResult> GetRequesterTrend([FromQuery] string granularity = "week", [FromQuery] int top = 5, [FromQuery] int days = 0)
        {
            try
            {
                var buckets = days > 0 ? BuildDayBuckets(days) : BuildBuckets(granularity);
                var startOfRange = buckets[0].Start;
                if (top <= 0) top = 5;

                var rows = await _requestService.GetAllRequestsQuery()
                    .Where(r => r.RequestDate >= startOfRange && r.RequesterName != null && r.RequesterName != "")
                    .Select(r => new { r.RequestDate, r.RequesterName })
                    .ToListAsync();

                var topNames = rows
                    .GroupBy(r => r.RequesterName)
                    .OrderByDescending(g => g.Count())
                    .Take(top)
                    .Select(g => g.Key)
                    .ToHashSet();

                var result = new List<object>();
                foreach (var b in buckets)
                {
                    foreach (var name in topNames)
                    {
                        var count = rows.Count(r => r.RequestDate >= b.Start && r.RequestDate < b.End && r.RequesterName == name);
                        result.Add(new { Label = b.Label, Name = name, Count = count });
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("VendorTrend")]
        public async Task<IActionResult> GetVendorTrend([FromQuery] string granularity = "week", [FromQuery] int top = 5)
        {
            try
            {
                var buckets = BuildBuckets(granularity);
                var startOfRange = buckets[0].Start;
                if (top <= 0) top = 5;

                var rows = await _requestService.GetAllRequestsQuery()
                    .Where(r => r.RequestDate >= startOfRange && r.VendorName != null && r.VendorName != "")
                    .Select(r => new { r.RequestDate, r.VendorName })
                    .ToListAsync();

                var topNames = rows
                    .GroupBy(r => r.VendorName)
                    .OrderByDescending(g => g.Count())
                    .Take(top)
                    .Select(g => g.Key)
                    .ToHashSet();

                var result = new List<object>();
                foreach (var b in buckets)
                {
                    foreach (var name in topNames)
                    {
                        var count = rows.Count(r => r.RequestDate >= b.Start && r.RequestDate < b.End && r.VendorName == name);
                        result.Add(new { Label = b.Label, Name = name, Count = count });
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("ValidityStatus")]
        public async Task<IActionResult> GetValidityStatus()
        {
            try
            {
                var today = DateTime.Today;
                var inOneMonth = today.AddMonths(1);

                var rows = await _requestService.GetAllRequestsQuery()
                    .Select(r => new { r.ValidUntil })
                    .ToListAsync();

                var active = rows.Count(r => r.ValidUntil == null || r.ValidUntil >= inOneMonth);
                var expiringSoon = rows.Count(r => r.ValidUntil != null && r.ValidUntil >= today && r.ValidUntil < inOneMonth);
                var expired = rows.Count(r => r.ValidUntil != null && r.ValidUntil < today);

                return Ok(new { active, expiringSoon, expired });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("ActiveVendors")]
        public async Task<IActionResult> GetActiveVendors([FromQuery] int top = 10)
        {
            try
            {
                var today = DateTime.Today;
                if (top <= 0) top = 10;

                var rows = await _requestService.GetAllRequestsQuery()
                    .Where(r => r.VendorName != null && r.VendorName != ""
                             && (r.ValidUntil == null || r.ValidUntil >= today))
                    .GroupBy(r => r.VendorName)
                    .Select(g => new { Name = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .Take(top)
                    .ToListAsync();

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private record Bucket(DateTime Start, DateTime End, string Label);

        private static List<Bucket> BuildBucketsForTrend(string timeframe, string aggregation)
        {
            var today = DateTime.Today;
            var tf = (timeframe ?? "7d").Trim().ToLowerInvariant();
            var agg = (aggregation ?? "day").Trim().ToLowerInvariant();

            var rangeStart = tf switch
            {
                "30d" => today.AddDays(-29),
                "6m"  => today.AddMonths(-6).AddDays(1),
                "1y"  => today.AddYears(-1).AddDays(1),
                _     => today.AddDays(-6), // 7d default
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
            else // day
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

        private static List<Bucket> BuildDayBuckets(int days)
        {
            var today = DateTime.Today;
            var buckets = new List<Bucket>();
            for (int i = days - 1; i >= 0; i--)
            {
                var s = today.AddDays(-i);
                buckets.Add(new Bucket(s, s.AddDays(1), s.ToString("d MMM", CultureInfo.InvariantCulture)));
            }
            return buckets;
        }

        private static List<Bucket> BuildBuckets(string granularity)
        {
            var today = DateTime.Today;
            var g = (granularity ?? "week").Trim().ToLowerInvariant();
            var buckets = new List<Bucket>();

            if (g == "year") // 12 months
            {
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                for (int i = 11; i >= 0; i--)
                {
                    var s = thisMonth.AddMonths(-i);
                    buckets.Add(new Bucket(s, s.AddMonths(1), s.ToString("MMM yy", CultureInfo.InvariantCulture)));
                }
            }
            else if (g == "month") // 4 weeks
            {
                var dow = (int)today.DayOfWeek;
                var daysToMonday = dow == 0 ? 6 : dow - 1;
                var thisMonday = today.AddDays(-daysToMonday);
                for (int i = 3; i >= 0; i--)
                {
                    var weekStart = thisMonday.AddDays(-7 * i);
                    var weekEnd = weekStart.AddDays(7);
                    var iso = ISOWeek.GetWeekOfYear(weekStart);
                    buckets.Add(new Bucket(weekStart, weekEnd, $"W{iso:D2}"));
                }
            }
            else // week — 7 days
            {
                for (int i = 6; i >= 0; i--)
                {
                    var s = today.AddDays(-i);
                    buckets.Add(new Bucket(s, s.AddDays(1), s.ToString("d MMM", CultureInfo.InvariantCulture)));
                }
            }

            return buckets;
        }
    }
}