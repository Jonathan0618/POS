using DevExpress.XtraEditors.DXErrorProvider;
using POS.Models.Security;
using POS.Services.Security;
using POS.Utility;
using POS.Validators;
using System;
using System.Threading.Tasks;

namespace POS.Forms.Security
{
    public partial class frmEditUser : DevExpress.XtraEditors.XtraForm
    {
        private readonly ControlMapper<UserDTO> _controlMapper;
        private readonly ModelValidator<UserDTO> _validator;
        private readonly UserService _userService;
        private readonly string _userId;
        public frmEditUser(string userId)
        {
            _controlMapper = new ControlMapper<UserDTO>();
            _validator = new ModelValidator<UserDTO>();
            _userService = new UserService();
            _userId = userId;
            InitializeComponent();
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            var user = new EditUserDTO();
            _controlMapper.MapToEntity(user, this);
            var validateResult = _validator.Validate(user, dxErrorProvider1, this, groupControl1);
            if (validateResult)
            {
                await _userService.UpdateUserAsync(user);
            }
        }

        private async void frmEditUser_Load(object sender, EventArgs e)
        {
            var roles = _userService.GetRoles();
            slueRole.Properties.DataSource = roles;

            var user = await _userService.GetUserByIdAsync(_userId);
            _controlMapper.MapToControl(user, this);
        }
    }
}