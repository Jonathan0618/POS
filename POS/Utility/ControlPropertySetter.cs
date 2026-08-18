using DevExpress.XtraEditors;
using System.Linq;
using System.Windows.Forms;

namespace POS.Utility
{
    public class ControlPropertySetter<T> where T : class
    {
        public void SetReadByEntity(bool isReadOnly, params Control[] parentControls)
        {
            var properties = typeof(T).GetProperties();
            foreach (Control parent in parentControls)
            {
                foreach (var property in properties)
                {
                    var control = FindControlByPropertyName(parent, property.Name);
                    if (control == null) continue;
                    SetControlToReadOnly(control, isReadOnly);
                }
            }
        }

        private void SetControlToReadOnly(Control childControl, bool isReadOnly)
        {
            var control = ((BaseEdit)childControl);
            control.Properties.ReadOnly = isReadOnly;
        }

        private Control FindControlByPropertyName(Control parent, string propertyName)
        {
            return parent.Controls.Cast<Control>()
                .FirstOrDefault(ctrl => ctrl.Name == $"txt{propertyName}" || ctrl.Name == $"lbl{propertyName}" ||
                ctrl.Name == $"de{propertyName}" || ctrl.Name == $"lue{propertyName}" || ctrl.Name == $"slue{propertyName}" ||
                ctrl.Name == $"ce{propertyName}" || ctrl.Name == $"se{propertyName}");
        }
    }
}
