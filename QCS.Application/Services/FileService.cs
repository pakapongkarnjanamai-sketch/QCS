using Microsoft.AspNetCore.Http;
using QCS.Application.Abstractions;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IFileService
    {
        /// <summary>
        /// ประมวลผลไฟล์ที่อัปโหลดและแปลงเป็น List ของ Quotation (พร้อม AttachmentFile)
        /// </summary>
        /// <param name="files">รายการไฟล์จาก Form</param>
        /// <param name="quotationsJson">JSON string ที่เก็บ Metadata ของไฟล์ (เช่น DocumentTypeId)</param>
        /// <returns>List ของ Quotation entity ที่พร้อมบันทึก</returns>
        Task<List<Quotation>> PrepareFilesForUploadAsync(List<IFormFile> files, string quotationsJson);
    }
    public class FileService : IFileService
    {
        // ค่า Default สำหรับ DocumentType กรณีไม่ระบุ
        private const int DefaultDocumentTypeId = 10;

        public async Task<List<Quotation>> PrepareFilesForUploadAsync(List<IFormFile> files, string quotationsJson)
        {
            var result = new List<Quotation>();

            if (files == null || files.Count == 0) return result;

            // แปลง JSON Metadata เป็น Object
            var metaList = string.IsNullOrEmpty(quotationsJson)
                ? new List<QuotationItemDto>()
                : JsonSerializer.Deserialize<List<QuotationItemDto>>(quotationsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var file in files.Where(f => f.Length > 0))
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                // หา Metadata ที่ชื่อไฟล์ตรงกัน
                var meta = metaList?.FirstOrDefault(m => m.FileName == file.FileName);

                // สร้าง Object Quotation
                var quotation = new Quotation
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    DocumentTypeId = meta?.DocumentTypeId ?? DefaultDocumentTypeId,
                    FilePath = "Database", // หรือระบุ path จริงถ้าเก็บแบบไฟล์

                    // สร้าง Object ลูก AttachmentFile (สำหรับเก็บ Binary Data)
                    AttachmentFile = new AttachmentFile
                    {
                        //FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = ms.ToArray()
                    }
                };

                result.Add(quotation);
            }

            return result;
        }
    }
}