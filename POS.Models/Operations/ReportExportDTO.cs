using System;

namespace POS.Models.Operations
{
    public sealed class ReportExportDTO
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
        public DateTime GeneratedUtc { get; set; }
    }
}
