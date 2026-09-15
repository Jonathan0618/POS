using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraReports.UI;
using System.Drawing;

namespace POS.Reports
{
    internal sealed class SettingsReceiptPreview : XtraReport
    {
        public SettingsReceiptPreview(string content)
        {
            DisplayName = "Sample receipt";
            PaperKind = DXPaperKind.Custom;
            PageWidthF = 315f; // Approximately 80 mm in hundredths of an inch.
            PageHeightF = 1100f;
            Margins = new DXMargins(10, 10, 10, 10);
            Font = new DXFont("Consolas", 9);
            var detail = new DetailBand { HeightF = 20f };
            detail.Controls.Add(new XRLabel
            {
                Text = content,
                Multiline = true,
                WordWrap = true,
                CanGrow = true,
                SizeF = new SizeF(295f, 20f)
            });
            Bands.Add(new TopMarginBand { HeightF = 10f });
            Bands.Add(detail);
            Bands.Add(new BottomMarginBand { HeightF = 10f });
        }
    }
}
