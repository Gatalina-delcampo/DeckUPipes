using System.Windows.Input;
using System.Windows.Interop;
using DeckUPipes.Interop;

namespace DeckUPipes.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xEC01;

    private readonly HwndSource _source;
    private readonly uint _modifiers;
    private readonly uint _virtualKey;

    public event Action? HotkeyPressed;

    public HotkeyService(string hotkeySpec)
    {
        (_modifiers, _virtualKey) = Parse(hotkeySpec);

        var parameters = new HwndSourceParameters("DeckUPipesHotkeyHost")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, _modifiers, _virtualKey))
        {
            _source.Dispose();
            throw new InvalidOperationException("the hotkey is already in use by another application");
        }
    }

    public static (uint Modifiers, uint VirtualKey) Parse(string hotkeySpec)
    {
        var parts = hotkeySpec.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid hotkey specification: '{hotkeySpec}'. Expected e.g. 'Control+Alt+M'.");
        }

        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "control" or "ctrl" => (uint)HotkeyModifiers.Control,
                "alt" => (uint)HotkeyModifiers.Alt,
                "shift" => (uint)HotkeyModifiers.Shift,
                "win" or "windows" => (uint)HotkeyModifiers.Win,
                _ => throw new ArgumentException($"Unknown modifier '{part}' in hotkey '{hotkeySpec}'."),
            };
        }

        var keyName = parts[^1];
        Key key;
        try
        {
            key = Enum.Parse<Key>(keyName, ignoreCase: true);
        }
        catch (Exception)
        {
            throw new ArgumentException($"Unknown key '{keyName}' in hotkey '{hotkeySpec}'.");
        }

        if (key == Key.None)
        {
            throw new ArgumentException($"Invalid key in hotkey '{hotkeySpec}'.");
        }

        modifiers |= NativeMethods.MOD_NOREPEAT;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return (modifiers, virtualKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source.Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        }

        _source.Dispose();
    }
}


