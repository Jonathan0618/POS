using POS.Models.Security;
using POS.Services.Security;
using DevExpress.XtraEditors;
using System.ComponentModel;
using System.Linq;

namespace POS.Forms.Security
{
    public partial class frmModules : DevExpress.XtraEditors.XtraForm
    {
        private readonly ModuleService _moduleService;
        public frmModules() : this(new ModuleService())
        {
        }

        public frmModules(ModuleService moduleService)
        {
            _moduleService = moduleService ?? throw new System.ArgumentNullException(nameof(moduleService));
            InitializeComponent();
            btnDeleteClaim.ButtonClick += btnDeleteModule_ButtonClick;
        }

        private void gridModules_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            var row = e.Row as ModuleDTO;
            if (row == null)
                return;

            var result = row.ModuleId == 0
                ? _moduleService.AddModule(row)
                : _moduleService.UpdateModule(row);
            if (!result.Succeeded)
            {
                XtraMessageBox.Show(
                    string.Join(System.Environment.NewLine, result.Errors),
                    "Module",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                ReloadModules();
            }
        }

        private void frmModules_Load(object sender, System.EventArgs e)
        {
            ReloadModules();
        }

        private void btnDeleteModule_ButtonClick(
            object sender,
            DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            var module = gridModules.GetFocusedRow() as ModuleDTO;
            if (module == null || module.ModuleId == 0)
                return;

            var confirmation = XtraMessageBox.Show(
                $"Delete module '{module.Name}'?",
                "Delete module",
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Question);
            if (confirmation != System.Windows.Forms.DialogResult.Yes)
                return;

            var result = _moduleService.DeleteModule(module);
            if (!result.Succeeded)
            {
                XtraMessageBox.Show(
                    string.Join(System.Environment.NewLine, result.Errors),
                    "Module cannot be deleted",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            ReloadModules();
        }

        private void ReloadModules()
        {
            var modules = _moduleService.GetAllModules();
            gcModules.DataSource = new BindingList<ModuleDTO>(modules.ToList());
        }
    }
}
