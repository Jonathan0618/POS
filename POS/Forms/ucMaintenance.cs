using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Operations;
using POS.Services.Maintenance;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms
{
    public sealed class ucMaintenance : XtraUserControl
    {
        private readonly DatabaseMaintenanceService _service;
        private readonly LabelControl lblHealth = new LabelControl { AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None };
        private readonly GridControl gcChecks = new GridControl();
        private readonly GridView gvChecks = new GridView();
        private readonly SimpleButton btnRefresh = new SimpleButton { Text = "Refresh Health" };
        private readonly SimpleButton btnOpenLogs = new SimpleButton { Text = "Open Log Folder" };
        private readonly SimpleButton btnBackup = new SimpleButton { Text = "Create Backup" };
        private readonly SimpleButton btnRestore = new SimpleButton { Text = "Restore to New Database" };
        private readonly LabelControl lblStatus = new LabelControl();
        private bool _busy;

        public ucMaintenance(DatabaseMaintenanceService service) { _service = service ?? throw new ArgumentNullException(nameof(service)); BuildUi(); }

        private void BuildUi()
        {
            Name = "ucMaintenance"; Dock = DockStyle.Fill;
            lblHealth.Dock = DockStyle.Top; lblHealth.Height = 82; lblHealth.Padding = new Padding(8);
            var actions = new PanelControl { Dock = DockStyle.Top, Height = 48 };
            btnRefresh.Location = new Point(8, 8); btnOpenLogs.Location = new Point(125, 8); btnBackup.Location = new Point(255, 8); btnRestore.Location = new Point(365, 8);
            actions.Controls.AddRange(new Control[] { btnRefresh, btnOpenLogs, btnBackup, btnRestore });
            gcChecks.Dock = DockStyle.Fill; gcChecks.MainView = gvChecks; gcChecks.ViewCollection.Add(gvChecks); gvChecks.OptionsBehavior.Editable = false; gvChecks.OptionsView.ShowGroupPanel = false; gvChecks.OptionsView.ShowAutoFilterRow = false;
            lblStatus.Dock = DockStyle.Bottom; lblStatus.Height = 28; lblStatus.Padding = new Padding(8);
            Controls.Add(gcChecks); Controls.Add(lblStatus); Controls.Add(actions); Controls.Add(lblHealth);
            btnRefresh.Click += async (s, e) => await LoadHealthAsync(); btnOpenLogs.Click += OpenLogs; btnBackup.Click += async (s, e) => await BackupAsync(); btnRestore.Click += async (s, e) => await RestoreAsync();
            Load += async (s, e) => await LoadHealthAsync();
        }

        private async Task LoadHealthAsync()
        {
            if (_busy || IsDisposed) return;
            try { SetBusy(true, "Checking database health..."); var health = await Task.Run(() => _service.GetHealth()); if (IsDisposed) return; lblHealth.Text = string.Format("Checked {0:g} | Application {1} | Database {2} | Migration {3}\r\nLog folder: {4}", health.CheckedUtc.ToLocalTime(), health.ApplicationVersion, health.DatabaseName, health.LatestApplicationMigration ?? "Unknown", health.LogDirectory); gcChecks.DataSource = new BindingList<HealthCheckItemDTO>(new System.Collections.Generic.List<HealthCheckItemDTO>(health.Checks)); lblStatus.Text = "Health check complete."; }
            catch (Exception ex) { ShowError(ex); }
            finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
        }

        private void OpenLogs(object sender, EventArgs e)
        {
            try { var health = _service.GetHealth(); if (!Directory.Exists(health.LogDirectory)) throw new DirectoryNotFoundException("The diagnostic log folder does not exist."); System.Diagnostics.Process.Start("explorer.exe", health.LogDirectory); }
            catch (Exception ex) { ShowError(ex); }
        }

        private async Task BackupAsync()
        {
            if (_busy) return;
            using (var dialog = new SaveFileDialog { Filter = "SQL Server backup (*.bak)|*.bak", DefaultExt = "bak", AddExtension = true, Title = "Create POS Backup" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { SetBusy(true, "Creating and verifying backup..."); var path = await Task.Run(() => _service.CreateBackup(dialog.FileName)); lblStatus.Text = "Backup created: " + path; }
                catch (Exception ex) { ShowError(ex); } finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
            }
        }

        private async Task RestoreAsync()
        {
            if (_busy) return;
            using (var open = new OpenFileDialog { Filter = "SQL Server backup (*.bak)|*.bak", CheckFileExists = true, Title = "Select POS Backup" })
            {
                if (open.ShowDialog(FindForm()) != DialogResult.OK) return;
                var name = new XtraInputBoxArgs { Caption = "Restore to New Database", Prompt = "Target database name", DefaultButtonIndex = 0 };
                {
                    var target = Convert.ToString(XtraInputBox.Show(name));
                    if (string.IsNullOrWhiteSpace(target)) return;
                    const string warning = "Restore creates a new database and never overwrites the live POS database.";
                    if (XtraMessageBox.Show(this, warning + "\r\n\r\nContinue?", "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try { SetBusy(true, "Restoring and checking the new database..."); var result = await Task.Run(() => _service.RestoreToNewDatabase(open.FileName, target.Trim())); lblStatus.Text = "Restore completed to database: " + result; }
                    catch (Exception ex) { ShowError(ex); } finally { if (!IsDisposed) SetBusy(false, lblStatus.Text); }
                }
            }
        }

        private void SetBusy(bool busy, string status) { _busy = busy; btnRefresh.Enabled = !busy; btnOpenLogs.Enabled = !busy; btnBackup.Enabled = !busy; btnRestore.Enabled = !busy; lblStatus.Text = status; UseWaitCursor = busy; }
        private static void ShowError(Exception ex) { var message = ex is InvalidOperationException || ex is System.ComponentModel.DataAnnotations.ValidationException || ex is UnauthorizedAccessException ? ex.Message : "The maintenance operation could not be completed. Please try again."; XtraMessageBox.Show(message, "Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
