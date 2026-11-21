using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

/// <summary>A modal sheet: dimmed scrim, spring-scaled card, danger tint for destructive actions.</summary>
public partial class DialogHost : UserControl
{
    public DialogHost()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is DialogViewModel { IsDanger: true })
            {
                IconBox.SetResourceReference(Border.BackgroundProperty, "Brush.DangerSoft");
                DialogIcon.SetResourceReference(Controls.Glyph.ForegroundProperty, "Brush.Danger");
            }

            if (Motion.IsOff)
            {
                return;
            }

            Motion.Animate(Scrim, OpacityProperty, 1, 260, Motion.EaseOut, 0);
            if (Card.RenderTransform is ScaleTransform scale)
            {
                Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 520, Motion.Spring, 0.9);
                Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 520, Motion.Spring, 0.9);
            }

            Motion.Animate(Card, OpacityProperty, 1, 220, Motion.EaseOut, 0);
        };
    }

    private void OnScrimClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DialogViewModel dialog && dialog is not RecoveryKeyDialogViewModel)
        {
            dialog.Close(null);
        }
    }
}
