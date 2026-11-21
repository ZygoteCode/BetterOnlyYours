using System.Windows;
using System.Windows.Controls;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;

namespace OnlyYours.App.Views;

public partial class LockView : UserControl
{
    private LockViewModel? _vm;

    public LockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as LockViewModel);
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(() => PasswordField.FocusField(), System.Windows.Threading.DispatcherPriority.Input);
            if (_vm?.JustLocked == true)
            {
                Emblem.PlayStamp();
            }
        };
        Unloaded += (_, _) => Attach(null);
    }

    private void Attach(LockViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.UnlockStarted -= OnStarted;
            _vm.UnlockFailed -= OnFailed;
            _vm.UnlockSucceeded -= OnSucceeded;
            _vm.PropertyChanged -= OnPropertyChanged;
        }

        _vm = vm;
        if (_vm is not null)
        {
            _vm.UnlockStarted += OnStarted;
            _vm.UnlockFailed += OnFailed;
            _vm.UnlockSucceeded += OnSucceeded;
            _vm.PropertyChanged += OnPropertyChanged;
        }
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LockViewModel.UseRecovery))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_vm?.UseRecovery == true)
                {
                    RecoveryField.FocusField();
                }
                else
                {
                    PasswordField.FocusField();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnStarted() => Emblem.SetBusy(true);

    private void OnFailed()
    {
        Emblem.PlayError();
        Motion.Shake(InputPanel);
        Dispatcher.BeginInvoke(() =>
        {
            if (_vm?.UseRecovery == true)
            {
                RecoveryField.FocusField();
                RecoveryField.SelectAll();
            }
            else
            {
                PasswordField.FocusField();
                PasswordField.SelectAll();
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnSucceeded() => Emblem.PlayUnlock();
}
