using DevExpress.Drawing;
using DevExpress.Pdf;
using PDF.Service.Interface;
using PDF.Service.Models;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PDF.Service.Services
{
    public class PdfGeneratorService : IPdfGeneratorService
    {
        private static readonly Regex RgbaRegex = new Regex(@"rgba\((\d+),\s*(\d+),\s*(\d+),\s*([0-9.]+)\)", RegexOptions.Compiled);
        private const float DefaultTargetWidthRatio = 0.35f;
        private const float DefaultEstimatedDetailsTextLength = 15f;
        private const float FontWidthFactor = 0.6f;
        private const float MinDynamicFontSize = 8f;
        private const float MaxDynamicFontSize = 100f;
        private const int WatermarkAlpha = 61;

        public PdfFile Merge(List<PdfFile> pdfFiles, string documentName)
        {
            using var pdfProcessor = new PdfDocumentProcessor();
            pdfProcessor.CreateEmptyDocument();

            foreach (var file in pdfFiles)
            {
                if (file.Data != null && file.Data.Length > 0)
                {
                    using var sourceStream = new MemoryStream(file.Data);
                    using var tempProcessor = new PdfDocumentProcessor();
                    tempProcessor.LoadDocument(sourceStream);
                    using var tempOutStream = new MemoryStream();
                    tempProcessor.SaveDocument(tempOutStream);
                    tempOutStream.Position = 0;
                    pdfProcessor.AppendDocument(tempOutStream);
                }
            }
            pdfProcessor.Document.Title = documentName;
            return SaveDocument(pdfProcessor, documentName);
        }

        public PdfFile Stamp(PdfFile pdfFile, ApprovalData approvalData, DrawSetting drawSetting, string referenceCode = "")
        {
            if (pdfFile.Data == null || pdfFile.Data.Length == 0) return pdfFile;

            using var pdfStream = new MemoryStream(pdfFile.Data);
            using var pdfProcessor = new PdfDocumentProcessor();
            pdfProcessor.LoadDocument(pdfStream);

            var stampProfile = GetStampProfile(pdfFile.DocumentTypeId, drawSetting.Color);

            foreach (var page in pdfProcessor.Document.Pages)
            {
                using var graphics = pdfProcessor.CreateGraphics();

                var pageDimensions = GetEffectivePageDimensions(page);
                float pageWidth = pageDimensions.Width;
                float estimatedTextLen = stampProfile.ShowDetails
                    ? DefaultEstimatedDetailsTextLength
                    : stampProfile.HeaderText.Length;
                float dynamicFontSize = (pageWidth * stampProfile.TargetWidthRatio) / (estimatedTextLen * FontWidthFactor);
                dynamicFontSize = Math.Clamp(dynamicFontSize, MinDynamicFontSize, MaxDynamicFontSize);

                var pageDrawSetting = new DrawSetting
                {
                    Color = stampProfile.Color,
                    FontSize = dynamicFontSize,
                    Margin = drawSetting.Margin,
                    AlignmentStamp = drawSetting.AlignmentStamp
                };

                if (stampProfile.ShowDetails)
                {
                    DrawWatermarkTable(page, graphics, approvalData, pageDrawSetting, stampProfile.HeaderText, referenceCode, true);
                }
                else
                {
                    DrawStampTable(page, graphics, approvalData, pageDrawSetting, stampProfile.HeaderText, referenceCode, false);
                }

                graphics.AddToPageForeground(page, 72, 72);
            }

            return SaveDocument(pdfProcessor, pdfFile.Name);
        }

        private void DrawStampTable(PdfPage pdfPage, PdfGraphics graphics, ApprovalData approvalData, DrawSetting drawSetting, string headerText, string referenceCode, bool showDetails)
        {
            var dims = CalculateTableDimensions(approvalData, drawSetting.FontSize, referenceCode, showDetails, headerText);
            var (startX, startY) = CalculateStartPosition(pdfPage, dims.TotalWidth, dims.TotalHeight, drawSetting.Margin, drawSetting.AlignmentStamp);
            Color baseColor = ParseColor(drawSetting.Color);
            Color fadedColor = Color.FromArgb(WatermarkAlpha, baseColor.R, baseColor.G, baseColor.B);
            DrawTableContent(graphics, startX, startY, dims, approvalData, drawSetting.FontSize, headerText, referenceCode, fadedColor, isWatermark: false, showDetails: showDetails);
        }

        private void DrawWatermarkTable(PdfPage pdfPage, PdfGraphics graphics, ApprovalData approvalData, DrawSetting drawSetting, string headerText, string referenceCode, bool showDetails)
        {
            float scaleFactor = 1.0f;
            float watermarkFontSize = drawSetting.FontSize * scaleFactor;

            var dims = CalculateTableDimensions(approvalData, watermarkFontSize, referenceCode, showDetails, headerText);

            Color baseColor = ParseColor(drawSetting.Color);
            Color watermarkColor = Color.FromArgb(WatermarkAlpha, baseColor.R, baseColor.G, baseColor.B);

            var (startX, startY) = CalculateStartPosition(
                pdfPage,
                dims.TotalWidth,
                dims.TotalHeight,
                drawSetting.Margin,
                drawSetting.AlignmentStamp
            );

            DrawTableContent(
                graphics,
                startX,
                startY,
                dims,
                approvalData,
                watermarkFontSize,
                headerText,
                referenceCode,
                watermarkColor,
                isWatermark: true,
                showDetails: showDetails
            );
        }

        private void DrawTableContent(PdfGraphics graphics, float startX, float startY, TableDimensions dims,
                                      ApprovalData approvalData, float baseFontSize, string headerText, string referenceCode,
                                      Color color, bool isWatermark, bool showDetails)
        {
            float borderThickness = isWatermark ? 3.0f : 1.5f;
            float innerThickness = isWatermark ? 1.5f : 0.5f;

            using var brushText = new DXSolidBrush(color);
            using var penBorder = new DXPen(color, borderThickness);
            using var penInner = new DXPen(color, innerThickness);

            var fontHeader = new DXFont("Arial", baseFontSize + 1, DXFontStyle.Bold);
            var formatCenter = new PdfStringFormat { Alignment = PdfStringAlignment.Center, LineAlignment = PdfStringAlignment.Center };

            graphics.DrawRectangle(penBorder, new RectangleF(startX, startY, dims.TotalWidth, dims.TotalHeight));

            var rectHeader = new RectangleF(startX, startY, dims.TotalWidth, dims.RowHeader);
            graphics.DrawString(headerText.ToUpper(), fontHeader, brushText, rectHeader, formatCenter);

            if (!showDetails) return;

            var fontStep = new DXFont("Arial", baseFontSize, DXFontStyle.Bold);
            var fontName = new DXFont("Arial", baseFontSize, DXFontStyle.Regular);
            var fontDate = new DXFont("Arial", baseFontSize - 2, DXFontStyle.Regular);
            var fontRef = new DXFont("Arial", baseFontSize - 2, DXFontStyle.Italic);

            var steps = approvalData.Step;
            int stepCount = steps?.Count ?? 0;
            if (stepCount == 0) return;

            float currentY = startY + dims.RowHeader;
            graphics.DrawLine(penInner, startX, currentY, startX + dims.TotalWidth, currentY);

            currentY += dims.RowStep;
            graphics.DrawLine(penInner, startX, currentY, startX + dims.TotalWidth, currentY);

            if (dims.HasRef)
            {
                float yRefStart = startY + dims.TotalHeight - dims.RowRef;
                graphics.DrawLine(penInner, startX, yRefStart, startX + dims.TotalWidth, yRefStart);
            }

            float yVerticalEnd = startY + dims.TotalHeight - dims.RowRef;
            for (int i = 1; i < stepCount; i++)
            {
                float x = startX + (i * dims.ColWidth);
                graphics.DrawLine(penInner, x, startY + dims.RowHeader, x, yVerticalEnd);
            }

            for (int i = 0; i < stepCount; i++)
            {
                var step = steps![i];
                float colX = startX + (i * dims.ColWidth);

                var rectStep = new RectangleF(colX, startY + dims.RowHeader, dims.ColWidth, dims.RowStep);
                graphics.DrawString(step.StepName, fontStep, brushText, rectStep, formatCenter);

                var rectName = new RectangleF(colX, startY + dims.RowHeader + dims.RowStep, dims.ColWidth, dims.RowName);
                float padding = baseFontSize * 0.2f;
                var rectNameContent = new RectangleF(colX, rectName.Y + padding, dims.ColWidth, rectName.Height - padding);
                graphics.DrawString(step.Approver, fontName, brushText, rectNameContent, formatCenter);

                float yDateStart = yVerticalEnd - dims.RowDate;
                var rectDate = new RectangleF(colX, yDateStart, dims.ColWidth, dims.RowDate);
                string dateStr = step.ApprovalDate.ToString("yyyy-MM-dd HH:mm");
                graphics.DrawString(dateStr, fontDate, brushText, rectDate, formatCenter);
            }

            if (dims.HasRef)
            {
                float yRefStart = startY + dims.TotalHeight - dims.RowRef;
                var rectRef = new RectangleF(startX, yRefStart, dims.TotalWidth, dims.RowRef);
                graphics.DrawString($"Ref: {referenceCode}", fontRef, brushText, rectRef, formatCenter);
            }
        }

        private static StampProfile GetStampProfile(int documentTypeId, string fallbackColor)
        {
            return documentTypeId switch
            {
                10 => new StampProfile("ORIGINAL QUOTATION", "#0000FF", true, 0.15f),
                50 => new StampProfile("EXPIRED", "#FF0000", false, 0.30f),
                20 => new StampProfile("COMPARED", "#FF0000", false, 0.30f),
                30 => new StampProfile("SPECIFICATIONS", "#000000", false, 0.30f),
                40 => new StampProfile("ATTACHMENT", "#000000", false, 0.30f),
                _ => new StampProfile("APPROVAL STAMP", fallbackColor, true, DefaultTargetWidthRatio)
            };
        }

        private readonly struct StampProfile
        {
            public StampProfile(string headerText, string color, bool showDetails, float targetWidthRatio)
            {
                HeaderText = headerText;
                Color = color;
                ShowDetails = showDetails;
                TargetWidthRatio = targetWidthRatio;
            }

            public string HeaderText { get; }
            public string Color { get; }
            public bool ShowDetails { get; }
            public float TargetWidthRatio { get; }
        }

        private struct TableDimensions
        {
            public float RowHeader, RowStep, RowName, RowDate, RowRef;
            public float ColWidth, TotalWidth, TotalHeight;
            public bool HasRef;
        }

        private TableDimensions CalculateTableDimensions(ApprovalData data, float baseFontSize, string refCode, bool showDetails, string headerText)
        {
            var d = new TableDimensions();
            d.HasRef = !string.IsNullOrEmpty(refCode);
            int count = data.Step?.Count ?? 0;

            d.RowHeader = baseFontSize * 2.0f;

            if (showDetails)
            {
                d.RowStep = baseFontSize * 1.8f;
                d.RowName = baseFontSize * 4.0f;
                d.RowDate = baseFontSize * 1.8f;
                d.RowRef = d.HasRef ? baseFontSize * 1.6f : 0f;

                d.TotalHeight = d.RowHeader + d.RowStep + d.RowName + d.RowDate + d.RowRef;
                d.ColWidth = Math.Max(85f, baseFontSize * 8f);
                d.TotalWidth = d.ColWidth * (count == 0 ? 1 : count);
            }
            else
            {
                d.RowStep = 0; d.RowName = 0; d.RowDate = 0; d.RowRef = 0;
                d.TotalHeight = d.RowHeader;

                float charWidthApprox = baseFontSize * 0.7f;
                float padding = baseFontSize * 2.0f;
                float estimatedTextWidth = headerText.Length * charWidthApprox;

                d.ColWidth = 0;
                d.TotalWidth = estimatedTextWidth + padding;
            }

            return d;
        }

        private (float startX, float startY) CalculateStartPosition(PdfPage pdfPage, float tableWidth, float tableHeight, float margin, AlignmentStamp alignment)
        {
            var pageDimensions = GetEffectivePageDimensions(pdfPage);
            float pageWidth = pageDimensions.Width;
            float pageHeight = pageDimensions.Height;
            float x = margin, y = margin;

            switch (alignment)
            {
                case AlignmentStamp.TopLeft: x = margin; y = margin; break;
                case AlignmentStamp.TopCenter: x = (pageWidth - tableWidth) / 2; y = margin; break;
                case AlignmentStamp.TopRight: x = pageWidth - tableWidth - margin; y = margin; break;
                case AlignmentStamp.CenterLeft: x = margin; y = (pageHeight - tableHeight) / 2; break;
                case AlignmentStamp.Center: x = (pageWidth - tableWidth) / 2; y = (pageHeight - tableHeight) / 2; break;
                case AlignmentStamp.CenterRight: x = pageWidth - tableWidth - margin; y = (pageHeight - tableHeight) / 2; break;
                case AlignmentStamp.BottomLeft: x = margin; y = pageHeight - tableHeight - margin; break;
                case AlignmentStamp.BottomCenter: x = (pageWidth - tableWidth) / 2; y = pageHeight - tableHeight - margin; break;
                case AlignmentStamp.BottomRight: x = pageWidth - tableWidth - margin; y = pageHeight - tableHeight - margin; break;
            }
            return (x, y);
        }

        private static PageDimensions GetEffectivePageDimensions(PdfPage page)
        {
            PdfRectangle box = GetPreferredPageBox(page);
            float width = (float)box.Width;
            float height = (float)box.Height;

            int rotation = NormalizeRotation((int)page.Rotate);
            if (rotation is 90 or 270)
            {
                (width, height) = (height, width);
            }

            return new PageDimensions(width, height);
        }

        private static PdfRectangle GetPreferredPageBox(PdfPage page)
        {
            if (IsValidBox(page.CropBox))
            {
                return page.CropBox;
            }

            if (IsValidBox(page.MediaBox))
            {
                return page.MediaBox;
            }

            return new PdfRectangle(0, 0, 595.28, 841.89);
        }

        private static bool IsValidBox(PdfRectangle box)
        {
            return box.Width > 0 && box.Height > 0;
        }

        private static int NormalizeRotation(int rotation)
        {
            rotation %= 360;
            if (rotation < 0)
            {
                rotation += 360;
            }

            return rotation;
        }

        private Color ParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString)) return Color.Black;
            if (colorString.StartsWith("#")) { try { return ColorTranslator.FromHtml(colorString); } catch { return Color.Black; } }
            var match = RgbaRegex.Match(colorString);
            if (match.Success)
            {
                int r = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int g = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int b = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                float a = float.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                return Color.FromArgb((int)(a * 255), r, g, b);
            }
            return Color.Black;
        }

        private readonly struct PageDimensions
        {
            public PageDimensions(float width, float height)
            {
                Width = width;
                Height = height;
            }

            public float Width { get; }
            public float Height { get; }
        }

        private PdfFile SaveDocument(PdfDocumentProcessor pdfProcessor, string fileName)
        {
            using var outputStream = new MemoryStream();
            pdfProcessor.SaveDocument(outputStream);
            return new PdfFile
            {
                Name = fileName,
                ContentType = "application/pdf",
                Data = outputStream.ToArray(),
                Length = outputStream.Length
            };
        }
    }
}