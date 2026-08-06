using Microsoft.EntityFrameworkCore;
using QCS.Application.Abstractions;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;

namespace QCS.Application.Services
{
    public interface IPaperSavedService
    {
        Task<PaperSavedDto> GetSummaryAsync();
        Task<List<PaperSavedTrendPointDto>> GetTrendAsync(string timeframe, string aggregation);
        Task<PaperSavedBackfillResultDto> BackfillPageCountsAsync(int batchSize);
    }

    public class PaperSavedService : IPaperSavedService
    {
        // PDF อ้างอิงต่อ A4 1 แผ่น (ค่ามาตรฐานสากล)
        private const double GramsCo2PerPage = 4.6;
        private const double LitersWaterPerPage = 10.0;
        private const int SheetsPerTree = 8333;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IPdfPageCounter _pdfPageCounter;

        public PaperSavedService(IUnitOfWork unitOfWork, IPdfPageCounter pdfPageCounter)
        {
            _unitOfWork = unitOfWork;
            _pdfPageCounter = pdfPageCounter;
        }

        public async Task<PaperSavedDto> GetSummaryAsync()
        {
            var approved = (int)RequestStatus.Completed;

            var quotationRepo = _unitOfWork.Repository<Quotation>();

            var query = quotationRepo.GetAll()
                .Where(q => q.Request != null
                            && q.Request.Status == approved
                            && q.AttachmentFile != null
                            && q.AttachmentFile.PageCount != null);

            var totalPages = await query.SumAsync(q => (int?)q.AttachmentFile!.PageCount!.Value) ?? 0;
            var quotationFiles = await query.CountAsync();

            var approvedRequestCount = await _unitOfWork.Repository<Request>().GetAll()
                .Where(r => r.Status == approved)
                .CountAsync();

            return BuildDto(totalPages, quotationFiles, approvedRequestCount);
        }

        public async Task<List<PaperSavedTrendPointDto>> GetTrendAsync(string timeframe, string aggregation)
        {
            var approved = (int)RequestStatus.Completed;
            var buckets = TrendBuckets.Build(timeframe, aggregation);
            var startOfRange = buckets[0].Start;

            var rows = await _unitOfWork.Repository<Quotation>().GetAll()
                .Where(q => q.Request != null
                            && q.Request.Status == approved
                            && q.Request.RequestDate >= startOfRange
                            && q.AttachmentFile != null
                            && q.AttachmentFile.PageCount != null)
                .Select(q => new
                {
                    Date = q.Request!.RequestDate,
                    Pages = q.AttachmentFile!.PageCount!.Value
                })
                .ToListAsync();

            return buckets.Select(b => new PaperSavedTrendPointDto
            {
                Label = b.Label,
                Year = b.Start.Year,
                Month = b.Start.Month,
                Pages = rows.Where(r => r.Date >= b.Start && r.Date < b.End).Sum(r => r.Pages)
            }).ToList();
        }

        public async Task<PaperSavedBackfillResultDto> BackfillPageCountsAsync(int batchSize)
        {
            if (batchSize <= 0) batchSize = 50;
            if (batchSize > 500) batchSize = 500;

            var repo = _unitOfWork.Repository<AttachmentFile>();

            var candidates = await repo.GetAll()
                .Where(a => a.PageCount == null && a.Data != null)
                .OrderBy(a => a.Id)
                .Take(batchSize)
                .ToListAsync();

            var updated = 0;
            var failed = 0;

            foreach (var att in candidates)
            {
                var pages = _pdfPageCounter.CountPages(att.Data, att.ContentType);
                if (pages.HasValue)
                {
                    att.PageCount = pages.Value;
                    updated++;
                }
                else
                {
                    // mark as zero so we don't retry it forever
                    att.PageCount = 0;
                    failed++;
                }
            }

            if (candidates.Count > 0)
            {
                await _unitOfWork.CommitAsync();
            }

            var remaining = await repo.GetAll().CountAsync(a => a.PageCount == null && a.Data != null);

            return new PaperSavedBackfillResultDto
            {
                Processed = candidates.Count,
                Updated = updated,
                Failed = failed,
                Remaining = remaining
            };
        }

        private static PaperSavedDto BuildDto(int totalPages, int quotationFiles, int approvedRequestCount)
        {
            return new PaperSavedDto
            {
                TotalPages = totalPages,
                QuotationFileCount = quotationFiles,
                ApprovedRequestCount = approvedRequestCount,
                Co2GramsSaved = totalPages * GramsCo2PerPage,
                WaterLitersSaved = totalPages * LitersWaterPerPage,
                TreesEquivalent = totalPages > 0 ? (double)totalPages / SheetsPerTree : 0d
            };
        }
    }
}
