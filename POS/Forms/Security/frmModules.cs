using POS.Models.Security;
using POS.Services.Security;
using System.ComponentModel;
using System.Linq;

namespace POS.Forms.Security
{
    public partial class frmModules : DevExpress.XtraEditors.XtraForm
    {
        private readonly ModuleService _moduleService;
        public frmModules()
        {
            _moduleService = new ModuleService();
            InitializeComponent();
        }

        private void gridModules_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            var row = e.Row as ModuleDTO;
            if(row.ModuleId == 0)
                _moduleService.AddModule(row);
            else
                _moduleService.UpdateModule(row);
        }

        private void frmModules_Load(object sender, System.EventArgs e)
        {
            var modules = _moduleService.GetAllModules();
            gcModules.DataSource = new BindingList<ModuleDTO>(modules.ToList());
        }
    }
}