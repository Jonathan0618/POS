using DevExpress.XtraEditors.DXErrorProvider;
using POS.Models.Security;
using POS.Services.Security;
using POS.Utility;
using POS.Validators;
using System;

namespace POS.Forms.Security
{
    public partial class frmEditUser : DevExpress.XtraEditors.XtraForm
    {
        private readonly ControlMapper<UserDTO> _controlMapper;
        private readonly ModelValidator<UserDTO> _validator;
        private readonly UserService _userService;
        public frmEditUser()
        {
            _controlMapper = new ControlMapper<UserDTO>();
            _validator = new ModelValidator<UserDTO>();
            _userService = new UserService();
            InitializeComponent();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var users = new EditUserDTO();
            _controlMapper.MapToEntity(users, this);
            var validateResult = _validator.Validate(users, dxErrorProvider1, this);
            if (validateResult)
            {
                //save
            }
        }

        private void frmEditUser_Load(object sender, EventArgs e)
        {
            var roles = _userService.GetRoles();
            slueRole.Properties.DataSource = roles;
        }
    }
}