using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using POS.Models.Operations;
using POS.Services.Security;
using POS.Core.Security;
using System.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucAudit : XtraUserControl
    {
        private readonly AuditService _service;
        private readonly AuditArchiveService _archives;
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly TextEdit _search = new TextEdit();
        private readonly TextEdit _entity = new TextEdit();
        private readonly TextEdit _action = new TextEdit();
        private readonly TextEdit _correlation = new TextEdit();
        private readonly ComboBoxEdit _category = new ComboBoxEdit();
        private readonly TextEdit _registerCode = new TextEdit();
        private readonly CheckEdit _unattributed = new CheckEdit { Text = "Only events without register attribution" };
        private readonly SimpleButton _apply = new SimpleButton { Text = "Search / Refresh" };
        private readonly SimpleButton _previous = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _next = new SimpleButton { Text = "Next" };
        private readonly SimpleButton _retentionButton = new SimpleButton { Text = "Retention summary" };
        private readonly SimpleButton _archiveButton = new SimpleButton { Text = "Export full archive" };
        private readonly SimpleButton _cancelExport = new SimpleButton { Text = "Cancel export", Enabled = false };
        private readonly SimpleButton _verifyArchive = new SimpleButton { Text = "Verify archive" };
        private readonly SimpleButton _restoreArchive = new SimpleButton { Text = "Restore to new database" };
        private CancellationTokenSource _exportCancellation;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 24, AutoSizeMode = LabelAutoSizeMode.None };
        private readonly MemoEdit _details = new MemoEdit { Dock = DockStyle.Fill };
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view = new GridView();
        private readonly LayoutControl _filters = new LayoutControl { Dock = DockStyle.Top, Height = 265 };
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private AuditSearchDTO _activeFilter;
        private bool _busy;
        private int _page = 1;
        private int _pages = 1;

        public ucAudit(AuditService service, AuditArchiveService archives)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _archives = archives ?? throw new ArgumentNullException(nameof(archives));
            Dock = DockStyle.Fill;
            _filters.Root = new LayoutControlGroup { GroupBordersVisible = false };
            AddFilter("From (local date)", _from);
            AddFilter("Through (local date)", _to);
            AddFilter("Category", _category);
            _category.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _category.Properties.Items.Add("All categories");
            _category.Properties.Items.AddRange(AuditCategories.Names.ToArray());
            _category.SelectedIndex = 0;
            AddFilter("Register code (exact)", _registerCode);
            AddFilter("", _unattributed);
            _registerCode.Properties.MaxLength = 30;
            _unattributed.CheckedChanged += (sender, args) =>
            {
                _registerCode.Enabled = !_unattributed.Checked;
                if (_unattributed.Checked) _registerCode.Text = "";
            };
            AddFilter("Username, user ID, or record ID", _search);
            AddFilter("Entity contains", _entity);
            AddFilter("Action contains", _action);
            AddFilter("Correlation ID (exact)", _correlation);
            _correlation.Properties.MaxLength = 36;
            var buttons = new PanelControl { Dock = DockStyle.Top, Height = 74 };
            _apply.SetBounds(10, 8, 130, 24);
            _previous.SetBounds(150, 8, 85, 24);
            _next.SetBounds(245, 8, 85, 24);
            _retentionButton.SetBounds(340, 8, 140, 24);
            buttons.Controls.AddRange(new Control[] { _apply, _previous, _next, _retentionButton });
            _archiveButton.SetBounds(10, 40, 145, 24);
            _cancelExport.SetBounds(165, 40, 110, 24);
            _archiveButton.Visible = _cancelExport.Visible = _archives.CanExport;
            buttons.Controls.AddRange(new Control[] { _archiveButton, _cancelExport });
            _verifyArchive.SetBounds(285, 40, 115, 24);
            _restoreArchive.SetBounds(410, 40, 175, 24);
            _restoreArchive.Visible = _archives.CanRestore;
            buttons.Controls.AddRange(new Control[] { _verifyArchive, _restoreArchive });
            _from.DateTime = DateTime.Today.AddDays(-7);
            _to.DateTime = DateTime.Today;
            _search.Properties.MaxLength = 200;
            _entity.Properties.MaxLength = 100;
            _action.Properties.MaxLength = 100;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsCustomization.AllowSort = false;
            _view.OptionsCustomization.AllowFilter = false;
            _details.Properties.ReadOnly = true;
            var detailsGroup = new GroupControl { Text = "Selected event details", Dock = DockStyle.Bottom, Height = 180 };
            detailsGroup.Controls.Add(_details);
            Controls.Add(_grid);
            Controls.Add(detailsGroup);
            Controls.Add(_status);
            Controls.Add(buttons);
            Controls.Add(_filters);
            _view.FocusedRowChanged += (sender, args) => ShowDetails();
            _apply.Click += async (sender, args) => await ReloadAsync(true);
            _retentionButton.Click += async (sender, args) => await ShowRetentionSummaryAsync();
            _archiveButton.Click += async (sender, args) => await ExportArchiveAsync();
            _cancelExport.Click += (sender, args) => _exportCancellation?.Cancel();
            _verifyArchive.Click += async (sender, args) => await RecoverArchiveAsync(false);
            _restoreArchive.Click += async (sender, args) => await RecoverArchiveAsync(true);
            _previous.Click += async (sender, args) => { if (!_busy && _page > 1) { _page--; await ReloadAsync(false); } };
            _next.Click += async (sender, args) => { if (!_busy && _page < _pages) { _page++; await ReloadAsync(false); } };
            Load += async (sender, args) => await ReloadAsync(true);
        }

        private void AddFilter(string label, Control editor)
        {
            _filters.Controls.Add(editor);
            _filters.Root.AddItem(label, editor);
        }

        private async Task ReloadAsync(bool applyFilters)
        {
            if (_busy || IsDisposed) return;
            _busy = true;
            _filters.Enabled = _apply.Enabled = _previous.Enabled = _next.Enabled = _retentionButton.Enabled = false;
            _archiveButton.Enabled = false;
            _verifyArchive.Enabled = _restoreArchive.Enabled = false;
            _status.Text = "Loading audit events...";
            try
            {
                if (applyFilters)
                {
                    _page = 1;
                    Guid correlationId;
                    if (!string.IsNullOrWhiteSpace(_correlation.Text) && !Guid.TryParse(_correlation.Text.Trim(), out correlationId))
                        throw new ValidationException("Enter a valid correlation ID or leave it blank.");
                    _activeFilter = new AuditSearchDTO
                    {
                        FromUtc = _from.EditValue == null ? (DateTime?)null : _from.DateTime.Date.ToUniversalTime(),
                        ToUtcExclusive = _to.EditValue == null ? (DateTime?)null : _to.DateTime.Date.AddDays(1).ToUniversalTime(),
                        Search = _search.Text, Entity = _entity.Text, Action = _action.Text,
                        Category = _category.SelectedIndex <= 0 ? null : _category.Text,
                        RegisterCode = _registerCode.Text, UnattributedOnly = _unattributed.Checked,
                        CorrelationId = string.IsNullOrWhiteSpace(_correlation.Text) ? (Guid?)null : Guid.Parse(_correlation.Text.Trim())
                    };
                }
                _activeFilter.PageNumber = _page;
                var result = await _service.SearchAsync(_activeFilter, _lifetime.Token);
                if (IsDisposed || _lifetime.IsCancellationRequested) return;
                _page = result.PageNumber;
                _pages = result.TotalPages;
                _grid.DataSource = result.Items;
                _view.PopulateColumns();
                foreach (var field in new[] { "DateLoggedUtc", "OldValue", "NewValue" })
                    _view.Columns[field].Visible = false;
                _view.Columns["LocalTime"].Caption = "Time (local)";
                _view.Columns["LocalTime"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                _view.Columns["LocalTime"].DisplayFormat.FormatString = "yyyy-MM-dd HH:mm:ss";
                _view.BestFitColumns();
                _status.Text = result.TotalCount == 0 ? "No audit events match these filters." :
                    $"Page {_page} of {_pages} ({result.TotalCount} events) - newest first";
                ShowDetails();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!IsDisposed)
                {
                    _grid.DataSource = null;
                    _details.Text = "";
                    _pages = _page = 1;
                    _status.Text = exception is ValidationException || exception is UnauthorizedAccessException
                        ? exception.Message : "Audit events could not be loaded. Use Search / Refresh to retry.";
                }
            }
            finally
            {
                _busy = false;
                if (!IsDisposed)
                {
                    _filters.Enabled = _apply.Enabled = _retentionButton.Enabled = true;
                    _archiveButton.Enabled = _archives.CanExport;
                    _verifyArchive.Enabled = true;
                    _restoreArchive.Enabled = _archives.CanRestore;
                    _previous.Enabled = _page > 1;
                    _next.Enabled = _page < _pages;
                }
                else _lifetime.Dispose();
            }
        }

        private async Task ShowRetentionSummaryAsync()
        {
            if (_busy || IsDisposed) return;
            _busy = true;
            _filters.Enabled = _apply.Enabled = _previous.Enabled = _next.Enabled = _retentionButton.Enabled = false;
            _archiveButton.Enabled = false;
            _verifyArchive.Enabled = _restoreArchive.Enabled = false;
            try
            {
                var summary = await _service.GetRetentionSummaryAsync(_lifetime.Token);
                if (IsDisposed || _lifetime.IsCancellationRequested) return;
                Func<DateTime?, string> localDate = value => value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "No events";
                XtraMessageBox.Show(FindForm(),
                    summary.Policy + Environment.NewLine + Environment.NewLine +
                    "All retained events: " + summary.TotalEvents.ToString("N0") + Environment.NewLine +
                    "Oldest event (local): " + localDate(summary.OldestEventUtc) + Environment.NewLine +
                    "Newest event (local): " + localDate(summary.NewestEventUtc) + Environment.NewLine + Environment.NewLine +
                    "This summary covers all audit events, regardless of the current search filters.",
                    "Audit retention", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!IsDisposed)
                    XtraMessageBox.Show(FindForm(), exception is UnauthorizedAccessException
                        ? exception.Message : "The retention summary could not be loaded. Please try again.",
                        "Audit retention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _busy = false;
                if (!IsDisposed)
                {
                    _filters.Enabled = _apply.Enabled = _retentionButton.Enabled = true;
                    _archiveButton.Enabled = _archives.CanExport;
                    _verifyArchive.Enabled = true;
                    _restoreArchive.Enabled = _archives.CanRestore;
                    _previous.Enabled = _page > 1;
                    _next.Enabled = _page < _pages;
                }
                else _lifetime.Dispose();
            }
        }

        private async Task ExportArchiveAsync()
        {
            if (_busy || IsDisposed) return;
            string destination;
            using (var dialog = new XtraSaveFileDialog
            {
                Title = "Export all retained audit events",
                Filter = "POS audit archive (*.zip)|*.zip",
                FileName = "POS-audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip",
                DefaultExt = "zip",
                AddExtension = true,
                OverwritePrompt = false
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                destination = dialog.FileName;
            }
            _busy = true;
            _filters.Enabled = _apply.Enabled = _previous.Enabled = _next.Enabled = _retentionButton.Enabled = _archiveButton.Enabled = false;
            _verifyArchive.Enabled = _restoreArchive.Enabled = false;
            _cancelExport.Enabled = true;
            _status.Text = "Exporting all retained audit events...";
            _exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            try
            {
                var progress = new Progress<long>(exportedCount =>
                {
                    if (!IsDisposed && _exportCancellation != null)
                        _status.Text = "Exporting audit events: " + exportedCount.ToString("N0");
                });
                var count = await _archives.ExportAsync(destination, progress, _exportCancellation.Token);
                if (!IsDisposed) _status.Text = $"Archive saved: {count:N0} events. Source records retained. {destination}";
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) _status.Text = "Archive export cancelled. Source records retained.";
            }
            catch (Exception exception)
            {
                if (!IsDisposed)
                    _status.Text = exception is UnauthorizedAccessException ? exception.Message :
                        "Archive could not be saved. Choose a new filename in a writable folder and retry.";
            }
            finally
            {
                _exportCancellation.Dispose();
                _exportCancellation = null;
                _busy = false;
                if (!IsDisposed)
                {
                    _cancelExport.Enabled = false;
                    _filters.Enabled = _apply.Enabled = _retentionButton.Enabled = true;
                    _archiveButton.Enabled = _archives.CanExport;
                    _verifyArchive.Enabled = true;
                    _restoreArchive.Enabled = _archives.CanRestore;
                    _previous.Enabled = _page > 1;
                    _next.Enabled = _page < _pages;
                }
                else _lifetime.Dispose();
            }
        }

        private async Task RecoverArchiveAsync(bool restore)
        {
            if (_busy || IsDisposed) return;
            string source;
            using (var dialog = new XtraOpenFileDialog
            {
                Title = restore ? "Restore an audit archive to a new database" : "Verify an audit archive",
                Filter = "POS audit archive (*.zip)|*.zip",
                Multiselect = false,
                CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                source = dialog.FileName;
            }
            if (restore && XtraMessageBox.Show(FindForm(),
                "Verify this archive and create a separate audit recovery database on the configured SQL Server? " +
                "The live POS database will remain unchanged.", "Restore audit archive",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _busy = true;
            _filters.Enabled = _apply.Enabled = _previous.Enabled = _next.Enabled = _retentionButton.Enabled = _archiveButton.Enabled = false;
            _verifyArchive.Enabled = _restoreArchive.Enabled = false;
            _cancelExport.Text = "Cancel";
            _cancelExport.Visible = true;
            _cancelExport.Enabled = true;
            _status.Text = restore ? "Verifying archive, then restoring to a new database..." : "Verifying archive...";
            _exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            try
            {
                var progress = new Progress<long>(count =>
                {
                    if (!IsDisposed && _exportCancellation != null)
                        _status.Text = "Archive records checked: " + count.ToString("N0") + (restore ? "; restore in progress..." : "");
                });
                var result = restore
                    ? await _archives.RestoreAsync(source, progress, _exportCancellation.Token)
                    : await _archives.VerifyAsync(source, progress, _exportCancellation.Token);
                if (!IsDisposed)
                {
                    _status.Text = restore ? "Archive restored to separate database: " + result : result;
                    XtraMessageBox.Show(FindForm(), _status.Text, "Audit archive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) _status.Text = "Archive operation cancelled.";
            }
            catch (Exception exception)
            {
                if (!IsDisposed)
                {
                    var message = exception is System.IO.InvalidDataException || exception is InvalidOperationException || exception is UnauthorizedAccessException
                        ? exception.Message : "The archive could not be processed. Check the file and SQL Server permissions, then retry.";
                    _status.Text = message;
                    XtraMessageBox.Show(FindForm(), message, "Audit archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _exportCancellation.Dispose();
                _exportCancellation = null;
                _busy = false;
                if (!IsDisposed)
                {
                    _cancelExport.Enabled = false;
                    _cancelExport.Text = "Cancel export";
                    _cancelExport.Visible = _archives.CanExport;
                    _filters.Enabled = _apply.Enabled = _retentionButton.Enabled = _verifyArchive.Enabled = true;
                    _archiveButton.Enabled = _archives.CanExport;
                    _verifyArchive.Enabled = true;
                    _restoreArchive.Enabled = _archives.CanRestore;
                    _previous.Enabled = _page > 1;
                    _next.Enabled = _page < _pages;
                }
                else _lifetime.Dispose();
            }
        }

        private void ShowDetails()
        {
            var selected = _view.GetFocusedRow() as AuditEventDTO;
            _details.Text = selected == null ? "" : "Before:" + Environment.NewLine +
                (selected.OldValue ?? "(none)") + Environment.NewLine + Environment.NewLine +
                "After:" + Environment.NewLine + (selected.NewValue ?? "(none)");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                _lifetime.Cancel();
                if (!_busy) _lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
