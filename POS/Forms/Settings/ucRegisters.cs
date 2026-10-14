using DevExpress.XtraEditors;
using POS.Forms.Security;
using POS.Services.Security;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace POS.Forms.Settings
{
    /// <summary>Employee and application-user registration workspace.</summary>
    public sealed class ucRegisters : XtraUserControl
    {
        private readonly ucUsers _users;

        public ucRegisters(UserService userService, Func<Form> createUserForm,
            Func<string, Form> createEditUserForm)
        {
            if (userService == null) throw new ArgumentNullException(nameof(userService));
            if (createUserForm == null) throw new ArgumentNullException(nameof(createUserForm));
            if (createEditUserForm == null) throw new ArgumentNullException(nameof(createEditUserForm));

            Name = "ucRegisters";
            Dock = DockStyle.Fill;
            var header = new PanelControl { Dock = DockStyle.Top, Height = 64 };
            var title = new LabelControl { Text = "Employee Registration", Location = new Point(14, 8) };
            title.Appearance.Font = new Font(title.Font.FontFamily, 16, FontStyle.Bold);
            var description = new LabelControl
            {
                Text = "Register employees as POS users, assign roles, and manage account access.",
                Location = new Point(16, 37)
            };
            header.Controls.AddRange(new Control[] { title, description });

            _users = new ucUsers(userService, createUserForm, createEditUserForm) { Dock = DockStyle.Fill };
            Controls.Add(_users);
            Controls.Add(header);
        }
    }
}
