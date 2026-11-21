using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using OnlyYours.App.Controls;
using OnlyYours.App.ViewModels;
using OnlyYours.Core.Security.Memory;
using Xunit;

namespace OnlyYours.App.Tests;

public class SecretTextBoxTests
{
    [Fact]
    public async Task MultilineSecretsAreLockedDown()
    {
        await Ui.Run(() =>
        {
            using var draft = SecretDraft.From("riga uno\nriga due");
            var box = new TextBox();
            SecretTextBox.SetDraft(box, draft);

            Assert.Equal("riga uno\nriga due", box.Text);
            Assert.False(box.IsUndoEnabled);
            Assert.False(box.AllowDrop);
            Assert.Equal(InputScopeNameValue.Password, ((InputScopeName)box.InputScope.Names[0]!).NameValue);

            box.Text = "new content";
            using (var lease = draft.Read())
            {
                Assert.Equal("new content", lease.Chars.ToString());
            }
        });
    }

    [Fact]
    public async Task CopyNeverReachesTheOrdinaryClipboard()
    {
        await Ui.Run(() =>
        {
            using var draft = SecretDraft.From("secret");
            var box = new TextBox();
            SecretTextBox.SetDraft(box, draft);
            box.SelectAll();

            // Without the protected clipboard service the command is swallowed, never forwarded.
            var service = AppServices.Clipboard;
            AppServices.Clipboard = null;
            try
            {
                var before = GetClipboardSequenceNumber();
                ApplicationCommands.Copy.Execute(null, box);
                ApplicationCommands.Cut.Execute(null, box);
                Assert.Equal(before, GetClipboardSequenceNumber());
                Assert.Equal("secret", box.Text);
            }
            finally
            {
                AppServices.Clipboard = service;
            }
        });
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
