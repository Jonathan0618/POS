using POS.Services.Security;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
   
    public partial class SignIn : DevExpress.XtraEditors.XtraForm
    {
        private readonly UserService _userService;
        public SignIn()
        {
            InitializeComponent();
            _userService = new UserService();
            
        }

        private void labelControl5_Click(object sender, EventArgs e)
        {

        }

        private async void btnSignIn_Click(object sender, EventArgs e)
        {
            var username = txtUserName.Text;
            var password = txtPassword.Text;

            var isSuccess = await _userService.Login(username, password);
            if (isSuccess) 
            { 
                DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid username or password.");
            }
        }
    }
}