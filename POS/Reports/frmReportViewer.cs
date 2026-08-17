using DevExpress.XtraReports.UI;

namespace POS.Reports
{
    public partial class frmReportViewer : DevExpress.XtraEditors.XtraForm
    {
        public frmReportViewer(XtraReport report)
        {
            InitializeComponent();

            documentViewer1.DocumentSource = report;
            report.CreateDocument();
        }
    }
}