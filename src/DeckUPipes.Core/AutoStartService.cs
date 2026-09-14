using Microsoft.Win32;

namespace DeckUPipes.Core;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeckUPipes";

    // The pre-rename build wrote its own Run entry. It is still honoured, and it is
    // cleaned up as soon as the current entry is written.
    private const string LegacyValueName = "EarClarinet";

    /// <summary>True when either the current or the pre-rename entry is present.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return HasValue(key, ValueName) || HasValue(key, LegacyValueName);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }

    private static bool HasValue(RegistryKey? key, string name) =>
        key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value);

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(ValueName, $"\"{exePath}\"");
                // Leave a single entry behind: drop the pre-rename one if it is there.
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
    }
}


