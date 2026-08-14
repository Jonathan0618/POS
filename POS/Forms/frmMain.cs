using DevExpress.XtraBars;
using POS.Services.Security;
using System;

namespace POS.Forms
{
    public partial class frmMain : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        readonly UserService _userService;
        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }

        private void btnCategory_ItemClick(object sender, ItemClickEventArgs e)
        {
            var frmCategory = new frmCategory();
            frmCategory.ShowDialog();
        }
    }
}