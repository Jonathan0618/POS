using POS.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public partial class SignIn : Form
    {
        private readonly UserService _userService;
        public SignIn()
        {
            InitializeComponent();
            var userService = new UserService();
            _userService = userService;
        }



        private void textBoxExt1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBoxExt1_TextChanged_1(object sender, EventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private async void sfButton1_Click(object sender, EventArgs e)
        {
            var username = txtUserName.Text;
            var password = txtPassword.Text;

            var isSuccess = await _userService.Login(username, password);
            if (isSuccess)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid username or password.", 
                "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
