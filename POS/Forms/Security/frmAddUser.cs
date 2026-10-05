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
        public frmAddUser() : this(new UserService())
        {
        }

        public frmAddUser(UserService userService)
        {
            _controlMapper = new ControlMapper<UserDTO>();
            _validator = new ModelValidator<UserDTO>();
            _userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
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

        private void slueRole_EditValueChanged(object sender, System.EventArgs e)
        {

        }
    }
}
