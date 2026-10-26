using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using POS.Models.Security;
using POS.Services.Security;
using POS.Services;
using POS.Core.Security;
using POS.Common.Enumerations;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace POS.Forms.Security
{
    public sealed class ucUsers : XtraUserControl
    {
        private readonly UserService _userService;
        private readonly Func<Form> _createAddUserForm;
        private readonly Func<string, Form> _createEditUserForm;
        private readonly GridControl _usersGrid = new GridControl();
        private readonly GridView _usersView = new GridView();
        private readonly GridControl _activityGrid = new GridControl();
        private readonly GridView _activityView = new GridView();
        private readonly GroupControl _activityGroup = new GroupControl
        {
            Text = "Recent User Activity",
            Dock = DockStyle.Bottom,
            Height = 230
        };
        private readonly SimpleButton _addButton = new SimpleButton { Text = "Add User" };
        private readonly SimpleButton _editButton = new SimpleButton { Text = "Edit User" };
        private readonly SimpleButton _toggleEnabledButton = new SimpleButton { Text = "Disable User" };
        private readonly TextEdit _searchText = new TextEdit();
        private readonly SimpleButton _searchButton = new SimpleButton { Text = "Search" };
        private readonly SimpleButton _previousButton = new SimpleButton { Text = "Previous" };
        private readonly SimpleButton _nextButton = new SimpleButton { Text = "Next" };
        private readonly LabelControl _pageLabel = new LabelControl();
        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canViewActivity;
        private const int PageSize = 25;
        private int _pageNumber = 1;
        private int _totalPages = 1;

        public ucUsers(
            UserService userService,
            Func<Form> createAddUserForm,
            Func<string, Form> createEditUserForm)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _createAddUserForm = createAddUserForm ?? throw new ArgumentNullException(nameof(createAddUserForm));
            _createEditUserForm = createEditUserForm ?? throw new ArgumentNullException(nameof(createEditUserForm));
            _canAdd = AuthorizationService.HasPermission(ResourceCodes.Users, ClaimActionType.Add);
            _canEdit = AuthorizationService.HasPermission(ResourceCodes.Users, ClaimActionType.Edit);
            _canViewActivity = AuthorizationService.HasPermission(ResourceCodes.Audit, ClaimActionType.View);

            Dock = DockStyle.Fill;
            var actions = new PanelControl { Dock = DockStyle.Top, Height = 84 };
            _addButton.Location = new System.Drawing.Point(10, 10);
            _editButton.Location = new System.Drawing.Point(105, 10);
            _toggleEnabledButton.Location = new System.Drawing.Point(200, 10);
            _searchText.Location = new System.Drawing.Point(10, 46);
            _searchText.Size = new System.Drawing.Size(240, 20);
            _searchText.Properties.NullValuePrompt = "Username, name, or role";
            _searchButton.Location = new System.Drawing.Point(258, 44);
            _previousButton.Location = new System.Drawing.Point(350, 44);
            _nextButton.Location = new System.Drawing.Point(435, 44);
            _pageLabel.Location = new System.Drawing.Point(525, 48);
            actions.Controls.AddRange(new Control[]
            {
                _addButton, _editButton, _toggleEnabledButton, _searchText,
                _searchButton, _previousButton, _nextButton, _pageLabel
            });

            _usersGrid.Dock = DockStyle.Fill;
            _usersGrid.MainView = _usersView;
            _usersGrid.ViewCollection.Add(_usersView);
            _usersView.OptionsBehavior.Editable = false;
            _usersView.OptionsView.ShowGroupPanel = false;
            _usersView.OptionsView.ShowAutoFilterRow = true;
            _usersView.FocusedRowChanged += async (sender, args) =>
            {
                UpdateToggleCaption();
                await LoadActivityAsync();
            };
            _usersView.DoubleClick += (sender, args) => EditSelectedUser();

            _activityGrid.Dock = DockStyle.Fill;
            _activityGrid.MainView = _activityView;
            _activityGrid.ViewCollection.Add(_activityView);
            _activityView.OptionsBehavior.Editable = false;
            _activityView.OptionsView.ShowGroupPanel = false;
            _activityView.OptionsView.ShowAutoFilterRow = true;
            _activityGroup.Controls.Add(_activityGrid);
            _activityGroup.Visible = _canViewActivity;

            Controls.Add(_usersGrid);
            Controls.Add(_activityGroup);
            Controls.Add(actions);
            _addButton.Click += (sender, args) => ShowEditor(_createAddUserForm());
            _editButton.Click += (sender, args) => EditSelectedUser();
            _toggleEnabledButton.Click += ToggleSelectedUser;
            _searchButton.Click += async (sender, args) =>
            {
                _pageNumber = 1;
                await ReloadAsync();
            };
            _searchText.KeyDown += async (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                args.SuppressKeyPress = true;
                _pageNumber = 1;
                await ReloadAsync();
            };
            _previousButton.Click += async (sender, args) =>
            {
                if (_pageNumber <= 1) return;
                _pageNumber--;
                await ReloadAsync();
            };
            _nextButton.Click += async (sender, args) =>
            {
                _pageNumber++;
                await ReloadAsync();
            };
            Load += async (sender, args) => await ReloadAsync();
        }

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            SetBusy(true);
            try
            {
                var page = await _userService.GetUsersPageAsync(
                    _searchText.Text,
                    _pageNumber,
                    PageSize);
                if (_pageNumber > page.TotalPages)
                {
                    _pageNumber = page.TotalPages;
                    page = await _userService.GetUsersPageAsync(
                        _searchText.Text,
                        _pageNumber,
                        PageSize);
                }

                _usersGrid.DataSource = new BindingList<UserDTO>(page.Items.ToList());
                _totalPages = page.TotalPages;
                _pageLabel.Text = $"Page {page.PageNumber} of {page.TotalPages} ({page.TotalCount} users)";
                _previousButton.Enabled = page.PageNumber > 1;
                _nextButton.Enabled = page.PageNumber < page.TotalPages;
                _usersView.BestFitColumns();
                UpdateToggleCaption();
                await LoadActivityAsync();
            }
            catch
            {
                XtraMessageBox.Show("Users could not be loaded.", "Users", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoadActivityAsync()
        {
            if (!_canViewActivity)
                return;
            var user = SelectedUser;
            if (user == null)
            {
                _activityGrid.DataSource = new BindingList<UserActivityDTO>();
                return;
            }

            try
            {
                var activity = await _userService.GetUserActivityAsync(user.UserId);
                if (SelectedUser?.UserId != user.UserId)
                    return;
                _activityGrid.DataSource = new BindingList<UserActivityDTO>(activity.ToList());
                _activityView.BestFitColumns();
                var idColumn = _activityView.Columns.ColumnByFieldName("ActivityId");
                if (idColumn != null) idColumn.Visible = false;
            }
            catch (UnauthorizedAccessException)
            {
                _activityGroup.Visible = false;
            }
            catch
            {
                _activityGrid.DataSource = new BindingList<UserActivityDTO>();
            }
        }

        private UserDTO SelectedUser => _usersView.GetFocusedRow() as UserDTO;

        private void EditSelectedUser()
        {
            var user = SelectedUser;
            if (user == null) return;
            ShowEditor(_createEditUserForm(user.UserId));
        }

        private void ShowEditor(Form form)
        {
            using (form) form.ShowDialog(FindForm());
            _ = ReloadAsync();
        }

        private async void ToggleSelectedUser(object sender, EventArgs e)
        {
            var user = SelectedUser;
            if (user == null) return;

            var enable = !user.IsActive;
            var prompt = enable ? "Enable this user?" : "Disable this user?";
            if (XtraMessageBox.Show(prompt, "Users", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            SetBusy(true);
            try
            {
                var result = await _userService.SetUserEnabledAsync(user.UserId, enable);
                if (!result.Succeeded)
                    XtraMessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Users", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await ReloadAsync();
            }
            catch
            {
                XtraMessageBox.Show("The account state could not be changed.", "Users", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateToggleCaption()
        {
            var user = SelectedUser;
            _toggleEnabledButton.Enabled = _canEdit && user != null;
            _editButton.Enabled = _canEdit && user != null;
            _toggleEnabledButton.Text = user?.IsActive == false ? "Enable User" : "Disable User";
        }

        private void SetBusy(bool busy)
        {
            _addButton.Enabled = !busy && _canAdd;
            _editButton.Enabled = !busy && _canEdit && SelectedUser != null;
            _toggleEnabledButton.Enabled = !busy && _canEdit && SelectedUser != null;
            _searchText.Enabled = !busy;
            _searchButton.Enabled = !busy;
            _previousButton.Enabled = !busy && _pageNumber > 1;
            _nextButton.Enabled = !busy && _pageNumber < _totalPages;
        }
    }
}
