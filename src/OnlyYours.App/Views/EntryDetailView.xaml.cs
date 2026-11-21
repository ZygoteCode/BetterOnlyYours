using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

public partial class EntryDetailView : UserControl
{
    private TotpViewModel? _totp;

    public EntryDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_totp is not null)
            {
                _totp.Rolled -= OnRolled;
            }

            _totp = (DataContext as EntryDetailViewModel)?.Totp;
            if (_totp is not null)
            {
                _totp.Rolled += OnRolled;
            }
        };
        Unloaded += (_, _) =>
        {
            if (_totp is not null)
            {
                _totp.Rolled -= OnRolled;
            }
        };
        SizeChanged += (_, _) => AdaptHeader();
    }

    /// <summary>On narrow panes the action buttons move under the title so it keeps its room.</summary>
    private void AdaptHeader()
    {
        var compact = ActualWidth < 640;
        Grid.SetRow(HeaderActions, compact ? 1 : 0);
        Grid.SetColumn(HeaderActions, compact ? 1 : 2);
        Grid.SetColumnSpan(HeaderActions, compact ? 2 : 1);
        HeaderActions.HorizontalAlignment = compact ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right;
        HeaderActions.Margin = compact ? new System.Windows.Thickness(12, 10, 0, 0) : new System.Windows.Thickness(0);
    }

    /// <summary>The new one-time code flips in like a split-flap display.</summary>
    private void OnRolled()
    {
        if (Motion.IsOff || TotpCode.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var flip = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        flip.KeyFrames.Add(new EasingDoubleKeyFrame(0.05, KeyTime.FromPercent(0.35), Motion.EaseIn));
        flip.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1), Motion.BackOut));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, flip);
    }
}
