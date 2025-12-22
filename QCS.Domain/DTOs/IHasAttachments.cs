using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs
{
    public interface IHasAttachments
    {
        List<IFormFile>? GetUploadFiles();
    }
}