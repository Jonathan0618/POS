using POS.Services.Security;
using System.Windows.Forms;

namespace POS
{
    public partial class Form1 : Form
    {
        private readonly UserService _userService;
        public Form1()
        {
            _userService = new UserService();
            InitializeComponent();
            _userService.
        }
    }
}
