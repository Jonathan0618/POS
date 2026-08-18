using DevExpress.XtraEditors.DXErrorProvider;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Forms;

namespace POS.Validators
{
    public class ModelValidator<T> where T : class
    {
        public bool Validate(T entity, DXErrorProvider errorProvider, params Control[] parentControls)
        {
            bool isValid = true;
            errorProvider.ClearErrors();
            var results = GetValidationResult(entity);
            if (results.Count > 0) isValid = false;

            foreach (var result in results)
            {
                foreach (var member in result.MemberNames)
                {
                    foreach (var parent in parentControls)
                    {
                        var control = FindControlByPropertyName(parent, member);
                        if (control != null)
                        {
                            errorProvider.SetError(control, result.ErrorMessage);
                        }
                    }
                }
            }
            return isValid;
        }

        private Control FindControlByPropertyName(Control parent, string propertyName)
        {
            return parent.Controls.Cast<Control>()
                .FirstOrDefault(ctrl => ctrl.Name == $"txt{propertyName}" || ctrl.Name == $"lbl{propertyName}" || ctrl.Name == $"de{propertyName}" || ctrl.Name == $"lue{propertyName}"
                || ctrl.Name == $"se{propertyName}" || ctrl.Name == $"memo{propertyName}");
        }

        private List<ValidationResult> GetValidationResult(T entity)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(entity, null, null);
            Validator.TryValidateObject(entity, context, results, true);
            return results;
        }
    }
}
