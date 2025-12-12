using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;

namespace QCS.Application.Services
{
    public interface IQuotationService
    {
        IQueryable<PurchaseRequest> GetQueryable();
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        // เพิ่ม method อื่นๆ เช่น DeleteQuotation, AddQuotation ถ้าต้องการแยกการจัดการไฟล์ออกจาก Form หลัก
    }
    public class QuotationService : IQuotationService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public QuotationService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        public IQueryable<PurchaseRequest> GetQueryable()
        {
          
            return _context.PurchaseRequests
         .Include(x => x.Quotations)
         .Include(x => x.ApprovalSteps)
         .AsNoTracking();
        }
        public async Task<AttachmentResultDto?> GetAttachmentAsync(int id)
        {
            var q = await _context.Quotations
                .Include(x => x.AttachmentFile)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (q == null) return null;

            // 1. Try DB
            if (q.AttachmentFile?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = q.AttachmentFile.Data,
                    ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
                    FileName = q.FileName
                };
            }

            // 2. Try Disk (Legacy Support)
            if (!string.IsNullOrEmpty(q.FilePath) && q.FilePath != "Database")
            {
                var path = Path.Combine(_env.WebRootPath, q.FilePath);
                if (System.IO.File.Exists(path))
                {
                    return new AttachmentResultDto
                    {
                        Data = await System.IO.File.ReadAllBytesAsync(path),
                        ContentType = q.ContentType ?? "application/octet-stream",
                        FileName = q.FileName
                    };
                }
            }

            return null;
        }
    }
}