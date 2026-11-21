using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnlyYours.App.Controls;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView() => InitializeComponent();
}

public partial class CreateVaultView : UserControl
{
    private CreateVaultViewModel? _vm;

    public CreateVaultView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnChanged;
            }

            _vm = DataContext as CreateVaultViewModel;
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnChanged;
            }
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CreateVaultViewModel.Step) || _vm is null)
        {
            return;
        }

        var bars = new[] { Step1Bar, Step2Bar, Step3Bar };
        for (var i = 0; i < bars.Length; i++)
        {
            bars[i].SetResourceReference(Border.BackgroundProperty, _vm.Step > i ? "Brush.AccentGradient" : "Brush.Line");
        }

        if (_vm.Step == 3)
        {
            Dispatcher.BeginInvoke(DoneSeal.PlayStamp, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}

public partial class MigrationView : UserControl
{
    public MigrationView() => InitializeComponent();
}

public partial class EntryEditorView : UserControl
{
    public EntryEditorView() => InitializeComponent();

    private void OnAddField(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EntryEditorViewModel editor)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = AddFieldButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var kind in editor.AddableKinds)
        {
            var item = new MenuItem { Header = EntryText.FieldName(kind) };
            Hint.SetIcon(item, (Geometry)FindResource(EntryText.FieldIcon(kind)));
            var captured = kind;
            item.Click += (_, _) => editor.AddField(captured);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }
}

public partial class SecurityView : UserControl
{
    public SecurityView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Adapt();
    }

    /// <summary>Narrow windows stack the score above the categories instead of squeezing both.</summary>
    private void Adapt()
    {
        var compact = ActualWidth < 1040;
        Grid.SetColumnSpan(ScoreCard, compact ? 3 : 1);
        Grid.SetRow(CategoryList, compact ? 1 : 0);
        Grid.SetColumn(CategoryList, compact ? 0 : 2);
        Grid.SetColumnSpan(CategoryList, compact ? 3 : 1);
        CategoryList.Margin = compact ? new System.Windows.Thickness(0, 18, 0, 0) : new System.Windows.Thickness(0);
    }
}

public partial class SharingView : UserControl
{
    public SharingView() => InitializeComponent();
}

public partial class TrashView : UserControl
{
    public TrashView() => InitializeComponent();
}

public partial class TransferView : UserControl
{
    public TransferView() => InitializeComponent();
}

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
}
