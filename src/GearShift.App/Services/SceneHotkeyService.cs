using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace GearShift.App.Services;

/// <summary>Registers Ctrl+Alt+1 through Ctrl+Alt+9 for the first nine scenes.</summary>
public sealed class SceneHotkeyService : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly Action<int> _activate;
    private readonly IntPtr _window;
    private readonly WndProc _wndProc;
    private readonly IntPtr _previousProc;

    public SceneHotkeyService(IntPtr window, DispatcherQueue queue, Action<int> activate)
    {
        _window = window;
        _queue = queue;
        _activate = activate;
        _wndProc = WindowProcedure;
        _previousProc = SetWindowLongPtr(_window, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));
        for (var i = 1; i <= 9; i++) RegisterHotKey(_window, i, 0x0002 | 0x0001, (uint)(0x30 + i));
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == 0x0312)
        {
            var index = wParam.ToInt32();
            if (index is >= 1 and <= 9) _queue.TryEnqueue(() => _activate(index - 1));
        }
        return CallWindowProc(_previousProc, hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        for (var i = 1; i <= 9; i++) UnregisterHotKey(_window, i);
        SetWindowLongPtr(_window, -4, _previousProc);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
