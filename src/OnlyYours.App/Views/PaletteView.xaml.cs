using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

public partial class PaletteView : UserControl
{
    public PaletteView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (!Motion.IsOff && Card.RenderTransform is ScaleTransform scale)
            {
                Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 420, Motion.Spring, 0.94);
                Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 420, Motion.Spring, 0.94);
                Motion.Animate(Card, OpacityProperty, 1, 180, Motion.EaseOut, 0);
            }

            Dispatcher.BeginInvoke(() =>
            {
                QueryBox.Focus();
                Keyboard.Focus(QueryBox);
            }, System.Windows.Threading.DispatcherPriority.Input);
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PaletteViewModel vm)
            {
                vm.Completed += Close;
            }
        };
    }

    private PaletteViewModel? Vm => DataContext as PaletteViewModel;

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                vm.Move(1);
                Results.ScrollIntoView(vm.Selected);
                e.Handled = true;
                break;
            case Key.Up:
                vm.Move(-1);
                Results.ScrollIntoView(vm.Selected);
                e.Handled = true;
                break;
            case Key.Enter:
                vm.Execute(null, Keyboard.Modifiers);
                e.Handled = true;
                break;
            case Key.System when e.SystemKey == Key.Enter:
                vm.Execute(null, ModifierKeys.Alt);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnResultClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Results, source) is ListBoxItem { DataContext: PaletteItem item })
        {
            Vm?.Execute(item, Keyboard.Modifiers);
        }
    }

    private void Close()
    {
        if (Window.GetWindow(this) is QuickSearchWindow quick)
        {
            quick.Close();
            return;
        }

        AppServices.App?.ClosePalette();
    }
}
