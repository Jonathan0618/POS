using DevExpress.XtraEditors;
using POS.Core.Attributes;
using POS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

public class AuthorizedForm : XtraForm
{
    private bool _authorizationHooksInstalled;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (!_authorizationHooksInstalled)
        {
            _authorizationHooksInstalled = true;
            InstallAuthorizationHooks();
        }
    }

    private void InstallAuthorizationHooks()
    {
        var methods = GetType()
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Where(m =>
                m.GetCustomAttributes(
                    typeof(ValidateAttribute),
                    true).Any())
            .ToList();

        foreach (var method in methods)
        {
            HookMethod(method);
        }
    }

    private void HookMethod(MethodInfo method)
    {
        string methodName = method.Name;

        // Expected:
        //
        // btnAdd_Click
        //
        // btnEdit_Click
        //
        // btnDelete_Click
        //
        int separatorIndex = methodName.LastIndexOf('_');

        if (separatorIndex <= 0 ||
            separatorIndex >= methodName.Length - 1)
        {
            return;
        }

        string controlName =
            methodName.Substring(0, separatorIndex);

        string eventName =
            methodName.Substring(separatorIndex + 1);

        object component =
            FindComponent(controlName);

        if (component == null)
        {
            MessageBox.Show(
                $"Unable to find control/component '{controlName}' " +
                $"for authorized event '{methodName}'.",
                "Authorization Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        EventInfo eventInfo =
            component.GetType().GetEvent(
                eventName,
                BindingFlags.Instance |
                BindingFlags.Public);

        if (eventInfo == null)
        {
            MessageBox.Show(
                $"Unable to find event '{eventName}' on " +
                $"'{component.GetType().Name}'.",
                "Authorization Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        if (eventInfo.EventHandlerType != typeof(EventHandler))
        {
            MessageBox.Show(
                $"Event '{methodName}' uses " +
                $"'{eventInfo.EventHandlerType.Name}'. " +
                $"Only EventHandler events are supported by " +
                $"AuthorizedForm.",
                "Authorization Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        var originalHandler =
            Delegate.CreateDelegate(
                typeof(EventHandler),
                this,
                method);

        // Remove the handler added by the WinForms Designer.
        eventInfo.RemoveEventHandler(
            component,
            originalHandler);

        // Create our wrapper.
        EventHandler authorizedHandler =
            (sender, e) =>
            {
                if (!CheckAuthorization(method))
                    return;

                method.Invoke(
                    this,
                    new object[] { sender, e });
            };

        // Add our authorized handler.
        eventInfo.AddEventHandler(
            component,
            authorizedHandler);
    }

    private bool CheckAuthorization(MethodInfo method)
    {
        var validations =
            method.GetCustomAttributes(
                typeof(ValidateAttribute),
                true)
            .Cast<ValidateAttribute>()
            .ToList();

        if (validations.Count == 0)
            return true;

        foreach (var validation in validations)
        {
            if (!AuthorizationService.HasPermission(
                    validation.Resource,
                    validation.Action))
            {
                MessageBox.Show(
                    $"You are not authorized to perform " +
                    $"'{validation.Action}' on " +
                    $"'{validation.Resource}'.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }
        }

        return true;
    }

    private object FindComponent(string name)
    {
        // Find normal WinForms controls.
        Control[] controls =
            Controls.Find(name, true);

        if (controls.Length > 0)
            return controls[0];

        // Find ToolStripItems and other components
        // through fields generated by the Designer.
        var field =
            FindField(GetType(), name);

        if (field != null)
        {
            return field.GetValue(this);
        }

        return null;
    }

    private FieldInfo FindField(
        Type type,
        string fieldName)
    {
        while (type != null)
        {
            var field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}