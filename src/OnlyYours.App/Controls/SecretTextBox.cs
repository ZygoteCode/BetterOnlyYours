using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using OnlyYours.App.Infrastructure;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.App.Controls;

/// <summary>
/// Binds a multi-line <see cref="TextBox"/> (notes, private keys) to a <see cref="SecretDraft"/>.
/// <para>
/// Multi-line editing needs WPF's text engine, so while such a field is open in the editor its
/// content exists as text inside the TextBox. Everything around that unavoidable window is
/// locked down: no undo stack keeping old fragments, a password input scope so the text
/// services, IMEs and Windows text suggestions never learn it, copy and cut routed through the
/// protected clipboard (kept out of clipboard history and cloud sync, cleared automatically),
/// no drag-out, and the box is emptied the moment the editor closes.
/// </para>
/// </summary>
public static class SecretTextBox
{
    public static readonly DependencyProperty DraftProperty = DependencyProperty.RegisterAttached(
        "Draft", typeof(SecretDraft), typeof(SecretTextBox), new PropertyMetadata(null, OnDraftChanged));

    private static readonly DependencyProperty SyncingProperty = DependencyProperty.RegisterAttached(
        "Syncing", typeof(bool), typeof(SecretTextBox), new PropertyMetadata(false));

    public static SecretDraft? GetDraft(DependencyObject d) => (SecretDraft?)d.GetValue(DraftProperty);

    public static void SetDraft(DependencyObject d, SecretDraft? value) => d.SetValue(DraftProperty, value);

    /// <summary>Tells TSF, IMEs and text prediction that this is a secret: nothing is learned or suggested.</summary>
    internal static InputScope PasswordScope()
    {
        var scope = new InputScope();
        scope.Names.Add(new InputScopeName(InputScopeNameValue.Password));
        return scope;
    }

    private static void OnDraftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        if (e.OldValue is null)
        {
            box.TextChanged += OnTextChanged;
            box.Unloaded += (_, _) => box.Clear();
            box.IsUndoEnabled = false;
            box.InputScope = PasswordScope();
            box.AllowDrop = false;
            SpellCheck.SetIsEnabled(box, false);
            DataObject.AddCopyingHandler(box, OnCopying);
            CommandManager.AddPreviewExecutedHandler(box, OnPreviewExecuted);
        }

        if (e.NewValue is SecretDraft draft && !draft.IsDisposed)
        {
            box.SetValue(SyncingProperty, true);
            using (var lease = draft.Read())
            {
                box.Text = lease.Chars.ToString();
            }

            box.SetValue(SyncingProperty, false);
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || (bool)box.GetValue(SyncingProperty) || GetDraft(box) is not { IsDisposed: false } draft)
        {
            return;
        }

        draft.Set(box.Text);
    }

    /// <summary>Copy and cut never touch the ordinary clipboard: the protected one hides and clears them.</summary>
    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (sender is not TextBox box || (e.Command != ApplicationCommands.Copy && e.Command != ApplicationCommands.Cut))
        {
            return;
        }

        e.Handled = true;
        if (box.SelectionLength == 0 || AppServices.Clipboard is not { } clipboard)
        {
            return;
        }

        var label = AutomationProperties.GetName(box);
        clipboard.CopySecret(box.SelectedText, string.IsNullOrWhiteSpace(label) ? Loc.T("FieldKind.Notes") : label);
        if (e.Command == ApplicationCommands.Cut && !box.IsReadOnly)
        {
            box.SelectedText = string.Empty;
        }
    }

    /// <summary>Dragging the selection out would hand the secret to another application unprotected.</summary>
    private static void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        if (e.IsDragDrop)
        {
            e.CancelCommand();
        }
    }
}
