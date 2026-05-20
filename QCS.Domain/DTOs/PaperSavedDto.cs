namespace QCS.Domain.DTOs
{
    public class PaperSavedDto
    {
        /// <summary>หน้ารวมจาก quotation PDF ของ Request ที่ approved แล้ว</summary>
        public int TotalPages { get; set; }

        /// <summary>จำนวนไฟล์ quotation ที่นำมานับ</summary>
        public int QuotationFileCount { get; set; }

        /// <summary>จำนวน Request ที่ approved (ตัวหารอ้างอิง)</summary>
        public int ApprovedRequestCount { get; set; }

        public double Co2GramsSaved { get; set; }
        public double WaterLitersSaved { get; set; }
        public double TreesEquivalent { get; set; }
    }

    public class PaperSavedTrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public int Pages { get; set; }
    }

    public class PaperSavedBackfillResultDto
    {
        public int Processed { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public int Remaining { get; set; }
    }
}
