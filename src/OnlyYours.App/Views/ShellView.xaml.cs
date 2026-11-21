using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnlyYours.App.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.App.Views;

public partial class ShellView : UserControl
{
    private ShellViewModel? _vm;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ShellViewModel);
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
            }, System.Windows.Threading.DispatcherPriority.Input);
            StaggerList();
        };
    }

    private void Attach(ShellViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.FocusSearchRequested -= FocusSearch;
            _vm.PropertyChanged -= OnChanged;
        }

        _vm = vm;
        if (_vm is null)
        {
            return;
        }

        _vm.FocusSearchRequested += FocusSearch;
        _vm.PropertyChanged += OnChanged;
        ApplyLayout();
        ListColumn.Width = new GridLength(_vm.App.Settings.ListWidth);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.RailCollapsed):
                ApplyLayout();
                break;
            case nameof(ShellViewModel.Selected):
                if (_vm?.Selected is { } item)
                {
                    EntryList.ScrollIntoView(item);
                }

                break;
            case nameof(ShellViewModel.SelectedFilter):
                StaggerList();
                break;
        }
    }

    private void ApplyLayout()
    {
        if (_vm is null)
        {
            return;
        }

        var collapsed = _vm.RailCollapsed;
        RailColumn.Width = new GridLength(collapsed ? 68 : 236);
        NewButton.Content = collapsed ? null : FindResource("Shell.New");
        TagsHeader.Visibility = collapsed || _vm.RailTags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    /// <summary>The first rows cascade in whenever the list content is replaced by a filter.</summary>
    private void StaggerList()
    {
        if (Motion.IsOff)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            for (var i = 0; i < Math.Min(EntryList.Items.Count, 14); i++)
            {
                if (EntryList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container)
                {
                    Anim.Play(container, "Rise", i * 28);
                }
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                if (_vm.Items.Count > 0)
                {
                    var index = _vm.Selected is null ? 0 : Math.Min(_vm.Items.IndexOf(_vm.Selected) + 1, _vm.Items.Count - 1);
                    _vm.Selected = _vm.Items[index];
                }

                e.Handled = true;
                break;
            case Key.Up:
                if (_vm.Items.Count > 0 && _vm.Selected is not null)
                {
                    var index = Math.Max(_vm.Items.IndexOf(_vm.Selected) - 1, 0);
                    _vm.Selected = _vm.Items[index];
                }

                e.Handled = true;
                break;
            case Key.Enter:
                if (_vm.Selected is null && _vm.Items.Count > 0)
                {
                    _vm.Selected = _vm.Items[0];
                }
                else if (_vm.Selected is { } selected)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        _vm.CopyField(selected.Entry, selected.Entry.PrimaryPassword);
                    }
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    {
                        _vm.CopyUsername(selected.Entry);
                    }
                }

                e.Handled = true;
                break;
        }
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.Selected is { } item && e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(EntryList, source) is ListBoxItem)
        {
            _vm.CopyField(item.Entry, item.Entry.PrimaryPassword);
        }
    }

    private void OnNewClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        NewMenu.Items.Clear();
        foreach (var kind in Enum.GetValues<EntryKind>())
        {
            var item = new MenuItem { Header = EntryText.KindName(kind) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "NewKind." + kind);
            Hint.SetIcon(item, (Geometry)FindResource(EntryText.KindIcon(kind)));
            var captured = kind;
            item.Click += (_, _) => _vm.NewEntry(captured);
            NewMenu.Items.Add(item);
        }

        NewMenu.PlacementTarget = NewButton;
        NewMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        NewMenu.IsOpen = true;
    }

    private void OnSplitterDragged(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        _vm.App.Settings.ListWidth = Math.Clamp(ListColumn.ActualWidth, 260, 560);
        _vm.App.SaveSettings();
    }
}
