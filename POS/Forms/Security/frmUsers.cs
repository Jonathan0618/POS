using POS.Common.Enumerations;
using POS.Core.Attributes;
using POS.Models.Security;
using POS.Services.Security;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public partial class frmUsers : AuthorizedForm
    {
        private readonly UserService _userService;
        public frmUsers()
        {
            _userService = new UserService();
            InitializeComponent();
        }

        private async void frmUsers_Load(object sender, System.EventArgs e)
        {
            var users = await _userService.GetUsers();
            gcUsers.DataSource = users;
        }
        [Validate("UserForm", ClaimActionType.Add)]
        private void btnAdd_Click(object sender, System.EventArgs e)
        {
            var frm = new frmAddUser();
            frm.ShowDialog();
        }

        [Validate("UserForm", ClaimActionType.Edit)]
        private void btnEdit_Click(object sender, System.EventArgs e)
        {
            var user = gridUsers.GetFocusedRow() as UserDTO;
            var frm = new frmEditUser(user.UserId);
            frm.ShowDialog();
        }

        [Validate("UserForm", ClaimActionType.Delete)]
        private void btnDelete_Click(object sender, System.EventArgs e)
        {
            if (MessageBox.Show("Delete this User?", "Confirmation", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) == DialogResult.OK)
            {
                //Delete
            }
        }
    }
}