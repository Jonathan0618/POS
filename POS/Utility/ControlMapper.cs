using DevExpress.XtraEditors;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace POS.Utility
{
    public class ControlMapper<T> where T : class
    {
        public void MapToControl(T entity, params Control[] controlParents)
        {
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                foreach (var parent in controlParents)
                {
                    var control = FindControlByPropertyName(parent, property.Name);
                    if (control == null) continue;
                    SetValue(property.GetValue(entity), control);
                }
            }
        }

        public void MapToEntity(T entity, Control parent)
        {
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                var control = FindControlByPropertyName(parent, property.Name);
                if (control == null) continue;
                SetPropertyValue(property, control, entity);
            }
        }
        private Control FindControlByPropertyName(Control parent, string propertyName)
        {
            return parent.Controls.Cast<Control>()
                .FirstOrDefault(ctrl => ctrl.Name == $"txt{propertyName}" || ctrl.Name == $"lbl{propertyName}" ||
                ctrl.Name == $"de{propertyName}" || ctrl.Name == $"lue{propertyName}" || ctrl.Name == $"slue{propertyName}" ||
                ctrl.Name == $"ce{propertyName}" || ctrl.Name == $"se{propertyName}" || ctrl.Name == $"memo{propertyName}");
        }

        public void SetValue(object value, Control control)
        {
            if (control is DateEdit dateEdit)
            {
                if (value == null) dateEdit.EditValue = null;
                else dateEdit.DateTime = (DateTime)value;
                return;
            }
            if (control is TextEdit textBox)
            {
                textBox.Text = value?.ToString() ?? string.Empty;
            }

            if (control is LabelControl label)
            {
                label.Text = value?.ToString() ?? string.Empty;
            }

            if (control is SearchLookUpEdit searchLookUpEdit)
            {
                if (value == null) searchLookUpEdit.EditValue = null;
                else searchLookUpEdit.EditValue = value;

                return;
            }

            if (control is LookUpEdit lookUpEdit)
            {
                if (value == null) lookUpEdit.EditValue = null;
                else lookUpEdit.EditValue = value;

                return;
            }

            if (control is CheckEdit checkEdit)
            {
                if (value == null) checkEdit.EditValue = null;
                else checkEdit.Checked = (bool)value;
            }

            if (control is SpinEdit spinEdit)
            {
                if (value == null) spinEdit.Value = 0;
                else spinEdit.Value = (decimal)value;
            }
        }
        private void SetPropertyValue(PropertyInfo property, Control control, T entity)
        {
            if (control is DateEdit dateEdit)
            {
                DateTime? timeValue = (DateTime?)dateEdit.EditValue;
                if (!(dateEdit.EditValue == null))
                {
                    property.SetValue(entity, timeValue);
                }
                return;
            }

            if (control is LookUpEdit lookupEdit)
            {
                object lueValue = lookupEdit.EditValue;
                Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var convertedValue = lueValue == null ? null : Convert.ChangeType(lueValue, targetType);
                property.SetValue(entity, convertedValue);
                return;
            }

            if (control is SearchLookUpEdit slueEdit)
            {
                object slueValue = slueEdit.EditValue;
                var convertedValue = slueValue == null ? null : Convert.ChangeType(slueValue, property.PropertyType);
                property.SetValue(entity, convertedValue);
                return;
            }

            if (control is SpinEdit spinEdit)
            {
                decimal seValue = spinEdit.Value;
                Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var convertedValue = Convert.ChangeType(seValue, targetType);
                property.SetValue(entity, convertedValue);
                return;
            }

            if (control is TextEdit textBox)
            {
                string textValue = textBox.Text;
                if (!string.IsNullOrEmpty(textValue))
                {
                    var convertedValue = Convert.ChangeType(textValue, property.PropertyType);
                    property.SetValue(entity, convertedValue);
                }
                return;
            }
        }
    }
}
