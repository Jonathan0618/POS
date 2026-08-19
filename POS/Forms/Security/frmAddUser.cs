using POS.Models.Security;
using POS.Services.Security;
using POS.Utility;
using POS.Validators;

namespace POS.Forms.Security
{
    public partial class frmAddUser : DevExpress.XtraEditors.XtraForm
    {
        private readonly ControlMapper<UserDTO> _controlMapper;
        private readonly ModelValidator<UserDTO> _validator;
        private readonly UserService _userService;
        public frmAddUser()
        {
            _controlMapper = new ControlMapper<UserDTO>();
            _validator = new ModelValidator<UserDTO>();
            _userService = new UserService();
            InitializeComponent();
        }

        private void frmAddUser_Load(object sender, System.EventArgs e)
        {
            var roles = _userService.GetRoles();
            slueRole.Properties.DataSource = roles;
        }

        private async void btnSave_Click(object sender, System.EventArgs e)
        {
            var user = new UserDTO();
            _controlMapper.MapToEntity(user, this);
            var validateResult = _validator.Validate(user, dxErrorProvider1, this);
            if(validateResult)
            {
                await _userService.AddUserAsync(user);
            }
        }
    }
}