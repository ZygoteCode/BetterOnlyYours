using System.Windows.Controls;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

public partial class GeneratorView : UserControl
{
    private GeneratorViewModel? _vm;

    public GeneratorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.Generated -= OnGenerated;
            }

            _vm = DataContext as GeneratorViewModel;
            if (_vm is not null)
            {
                _vm.Generated += OnGenerated;
            }
        };
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => Output.Scramble(900), System.Windows.Threading.DispatcherPriority.Loaded);
        SizeChanged += (_, _) => Adapt();
        Unloaded += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.Generated -= OnGenerated;
            }
        };
    }

    /// <summary>Every new password "decrypts" itself on screen.</summary>
    private void OnGenerated() => Dispatcher.BeginInvoke(() => Output.Scramble(), System.Windows.Threading.DispatcherPriority.Loaded);

    /// <summary>Below ~1100 px the session history moves under the generator instead of squeezing it.</summary>
    private void Adapt()
    {
        var compact = ActualWidth < 1100;
        Layout.ColumnDefinitions[1].Width = new System.Windows.GridLength(compact ? 0 : 24);
        Layout.ColumnDefinitions[2].Width = new System.Windows.GridLength(compact ? 0 : 340);
        Grid.SetColumn(HistoryCard, compact ? 0 : 2);
        Grid.SetRow(HistoryCard, compact ? 1 : 0);
        HistoryCard.Margin = compact ? new System.Windows.Thickness(0, 18, 0, 0) : new System.Windows.Thickness(0, 78, 0, 0);
    }
}
