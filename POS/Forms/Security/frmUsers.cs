using POS.Services.Security;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public partial class frmUsers : DevExpress.XtraEditors.XtraForm
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

        private void btnAdd_Click(object sender, System.EventArgs e)
        {
            var frm = new frmAddUser();
            frm.ShowDialog();
        }

        private void btnEdit_Click(object sender, System.EventArgs e)
        {
            var frm = new frmEditUser();
            frm.ShowDialog();
        }

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