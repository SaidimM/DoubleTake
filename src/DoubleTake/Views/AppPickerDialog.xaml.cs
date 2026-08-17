using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace QuickTranslator.Views
{
    public sealed partial class AppPickerDialog : ContentDialog
    {
        private List<DiscoveredAppItem> _allRunningApps = new List<DiscoveredAppItem>();
        private List<DiscoveredAppItem> _allInstalledApps = new List<DiscoveredAppItem>();
        private bool _showingRunning = true;
        private readonly HashSet<string> _selectedExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdatingSelection = false;

        public List<string> SelectedProcessExes { get; private set; } = new List<string>();

        public AppPickerDialog(IEnumerable<string> initialExclusions = null)
        {
            this.InitializeComponent();
            if (initialExclusions != null)
            {
                foreach (var exe in initialExclusions)
                {
                    if (!string.IsNullOrWhiteSpace(exe))
                        _selectedExes.Add(exe.Trim());
                }
            }
            this.Loaded += AppPickerDialog_Loaded;
        }

        private async void AppPickerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            AppsListView.Visibility = Visibility.Collapsed;

            var runningTask = AppDiscoveryService.GetRunningAppsAsync();
            var installedTask = AppDiscoveryService.GetInstalledAppsAsync();

            await Task.WhenAll(runningTask, installedTask);

            _allRunningApps = runningTask.Result;
            _allInstalledApps = installedTask.Result;

            RunningTabCountText.Text = $"Running Windows ({_allRunningApps.Count})";
            InstalledTabCountText.Text = $"Installed Apps ({_allInstalledApps.Count})";

            LoadingPanel.Visibility = Visibility.Collapsed;
            AppsListView.Visibility = Visibility.Visible;

            UpdateTabHighlight();
            FilterAndRefreshList();
        }

        private void RunningTabButton_Click(object sender, RoutedEventArgs e)
        {
            _showingRunning = true;
            UpdateTabHighlight();
            FilterAndRefreshList();
        }

        private void InstalledTabButton_Click(object sender, RoutedEventArgs e)
        {
            _showingRunning = false;
            UpdateTabHighlight();
            FilterAndRefreshList();
        }

        private void UpdateTabHighlight()
        {
            RunningTabButton.FontWeight = _showingRunning ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            InstalledTabButton.FontWeight = !_showingRunning ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAndRefreshList();
        }

        private void FilterAndRefreshList()
        {
            var baseList = _showingRunning ? _allRunningApps : _allInstalledApps;
            string query = SearchBox.Text?.Trim();

            List<DiscoveredAppItem> filtered;
            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = baseList.ToList();
            }
            else
            {
                filtered = baseList.Where(x =>
                    (x.Name != null && x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (x.ExeName != null && x.ExeName.Contains(query, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            _isUpdatingSelection = true;
            AppsListView.ItemsSource = filtered;

            // Re-apply selections for visible items
            AppsListView.SelectedItems.Clear();
            foreach (var item in filtered)
            {
                if (_selectedExes.Contains(item.ExeName))
                {
                    AppsListView.SelectedItems.Add(item);
                }
            }
            _isUpdatingSelection = false;

            UpdateSelectionCount();
            EmptyStateText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AppsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            foreach (var item in e.AddedItems)
            {
                if (item is DiscoveredAppItem app && !string.IsNullOrWhiteSpace(app.ExeName))
                {
                    _selectedExes.Add(app.ExeName);
                }
            }

            foreach (var item in e.RemovedItems)
            {
                if (item is DiscoveredAppItem app && !string.IsNullOrWhiteSpace(app.ExeName))
                {
                    _selectedExes.Remove(app.ExeName);
                }
            }

            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int count = _selectedExes.Count;
            SelectedCountText.Text = $"{count} application{(count == 1 ? "" : "s")} excluded";
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedProcessExes = _selectedExes.ToList();
        }
    }
}
