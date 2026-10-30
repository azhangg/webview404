using System.Runtime.InteropServices;
using System.Windows.Input;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 全局低级键盘钩子。WebView2 的键盘输入直接进入其子 HWND，WPF 的键盘事件收不到，
    /// 所以用 WH_KEYBOARD_LL 捕获快捷键；只在前台窗口属于本进程时处理，不影响其它程序。
    /// </summary>
    public sealed class KeyboardInputHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelKeyboardProc _proc;
        private readonly uint _processId = (uint)Environment.ProcessId;
        private IntPtr _hook = IntPtr.Zero;
        private bool _disposed;

        public KeyboardInputHook()
        {
            _proc = HookCallback;
        }

        /// <summary>按键按下回调，返回 true 表示吞掉该按键。</summary>
        public Func<Key, bool>? KeyDownFilter { get; set; }

        /// <summary>按键抬起回调，返回 true 表示吞掉该按键。</summary>
        public Func<Key, bool>? KeyUpFilter { get; set; }

        /// <summary>
        /// 安装钩子。失败返回 false，调用方应降级到其它输入通路。
        /// </summary>
        public bool TryInstall()
        {
            if (_hook != IntPtr.Zero)
            {
                return true;
            }

            if (_disposed)
            {
                return false;
            }

            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && IsForegroundFromThisProcess())
                {
                    var message = wParam.ToInt32();
                    var isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                    var isUp = message is WM_KEYUP or WM_SYSKEYUP;

                    if (isDown || isUp)
                    {
                        var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                        var key = KeyInterop.KeyFromVirtualKey((int)info.VirtualKeyCode);
                        var handled = isDown
                            ? KeyDownFilter?.Invoke(key) == true
                            : KeyUpFilter?.Invoke(key) == true;

                        if (handled)
                        {
                            return 1;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 钩子回调内不能抛异常，忽略本次处理，继续交给后续钩子
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private bool IsForegroundFromThisProcess()
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(foreground, out var processId);
            return processId == _processId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint VirtualKeyCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
