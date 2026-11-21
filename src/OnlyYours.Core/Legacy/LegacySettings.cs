using System.Text.Json;
using OnlyYours.Core.Settings;

namespace OnlyYours.Core.Legacy;

/// <summary>Carries the predecessor's preferences (<c>settings.json</c>) over to OnlyYours.</summary>
public static class LegacySettings
{
    public static int Apply(string path, AppSettings target)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return Apply(json.RootElement, target);
        }
        catch
        {
            return 0;
        }
    }

    public static int Apply(JsonElement root, AppSettings target)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        var applied = 0;
        if (root.TryGetProperty("reduceMotion", out var reduce) && reduce.ValueKind == JsonValueKind.True)
        {
            target.Motion = "reduced";
            target.Intro = "short";
            applied++;
        }

        if (TryInt(root, "autoLockMinutes", out var autoLock))
        {
            target.AutoLockMinutes = AppSettings.AutoLockChoices.Contains(autoLock) ? autoLock : AppSettings.AutoLockChoices.OrderBy(c => Math.Abs(c - autoLock)).First();
            applied++;
        }

        if (TryInt(root, "clipboardClearSeconds", out var clipboard))
        {
            target.ClipboardClearSeconds = AppSettings.ClipboardChoices.Contains(clipboard) ? clipboard : AppSettings.ClipboardChoices.OrderBy(c => Math.Abs(c - clipboard)).First();
            applied++;
        }

        applied += ApplyBool(root, "confirmDelete", v => target.ConfirmDelete = v);
        applied += ApplyBool(root, "keepPasswordHistory", v => target.KeepPasswordHistory = v);
        applied += ApplyBool(root, "globalHotkeyEnabled", v => target.GlobalHotkeys = v);

        if (root.TryGetProperty("generator", out var generator) && generator.ValueKind == JsonValueKind.Object)
        {
            var g = target.Generator;
            if (generator.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String)
            {
                g.Mode = mode.GetString() == "passphrase" ? GeneratorMode.Passphrase : GeneratorMode.Characters;
            }

            if (TryInt(generator, "length", out var length))
            {
                g.Length = length;
            }

            ApplyBool(generator, "lowercase", v => g.Lowercase = v);
            ApplyBool(generator, "uppercase", v => g.Uppercase = v);
            ApplyBool(generator, "digits", v => g.Digits = v);
            ApplyBool(generator, "symbols", v => g.Symbols = v);
            ApplyBool(generator, "avoidAmbiguous", v => g.AvoidAmbiguous = v);
            ApplyBool(generator, "capitalizeWords", v => g.CapitalizeWords = v);
            ApplyBool(generator, "appendNumber", v => g.AppendNumber = v);
            if (TryInt(generator, "words", out var words))
            {
                g.Words = words;
            }

            if (generator.TryGetProperty("separator", out var separator) && separator.ValueKind == JsonValueKind.String)
            {
                g.Separator = separator.GetString() ?? "-";
            }

            g.WordList = "en";
            g.Normalize();
            applied++;
        }

        target.Normalize();
        return applied;
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }

    private static int ApplyBool(JsonElement element, string name, Action<bool> apply)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            apply(property.GetBoolean());
            return 1;
        }

        return 0;
    }
}
