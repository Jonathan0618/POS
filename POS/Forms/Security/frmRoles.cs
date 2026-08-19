using POS.Domains.Security;
using POS.Models.Security;
using POS.Services.Security;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public partial class frmRoles : DevExpress.XtraEditors.XtraForm
    {
        private readonly RoleService _roleService;
        public frmRoles()
        {
            _roleService = new RoleService();
            InitializeComponent();
        }

        private void frmRoles_Load(object sender, EventArgs e)
        {
            LoadRoles();
        }

        private void LoadRoles()
        {
            var roles = _roleService.GetRoles();
            gcRoles.DataSource = new BindingList<RoleDTO>(roles.ToList());
        }

        private void LoadClaims(string roleId)
        {
            var claims = _roleService.GetClaims(roleId);
            gcPermissions.DataSource = new BindingList<RoleClaimDTO>(claims.ToList());
        }

        private void btnAddRole_Click(object sender, EventArgs e)
        {
            gridRoles.AddNewRow();
        }

        private void btnAddPermission_Click(object sender, EventArgs e)
        {
            gridPermissions.AddNewRow();
        }

        private void gridRoles_FocusedRowObjectChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowObjectChangedEventArgs e)
        {
            if (e.Row == null)
                return;

            var role = e.Row as RoleDTO;
            LoadClaims(role.RoleId);
        }

        private void gridRoles_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            var row = e.Row as RoleDTO;

            if(row.RoleId == null)
                _roleService.AddRole(row);
            else
                _roleService.UpdateRole(row);
        }

        private void gridPermissions_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            var row = e.Row as RoleClaimDTO;
            var roleRow = gridRoles.GetFocusedRow() as RoleDTO;

            if (row.RoleClaimId == null)
            {
                row.RoleId = roleRow.RoleId;
                _roleService.AddClaim(row);
            }
            else
                _roleService.UpdateClaim(row);
        }

        private void btnDeleteRole_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("Delete this Role?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {

            }
        }

        private void btnDeleteClaim_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete this Permission?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {

            }
        }
    }
}