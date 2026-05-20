namespace QCS.Application.Abstractions
{
    /// <summary>
    /// นับจำนวนหน้าของไฟล์ PDF จาก byte array
    /// คืน null ถ้าไม่สามารถอ่านได้ (ไฟล์เสีย, ไม่ใช่ PDF, encrypted ฯลฯ)
    /// </summary>
    public interface IPdfPageCounter
    {
        int? CountPages(byte[]? data, string? contentType);
    }
}
