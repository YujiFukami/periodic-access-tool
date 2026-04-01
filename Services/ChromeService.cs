using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PeriodicAccessTool.Services
{
    /// <summary>
    /// Google Chrome の起動・URLオープン・ページクローズを担当するサービス。
    /// ウィンドウハンドル(HWND)で本アプリが開いたウィンドウを識別して閉じる。
    /// </summary>
    public class ChromeService
    {
        // --- Win32 API ---
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        // ---- 内部状態 ----
        // HWND -> URL  (本アプリが開いたウィンドウ)
        private readonly Dictionary<IntPtr, string> _managedWindows = new();

        public string FindChromePath(string overridePath = "")
        {
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                return overridePath;

            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google", "Chrome", "Application", "chrome.exe"),
            };
            return candidates.FirstOrDefault(File.Exists) ?? "";
        }

        public bool IsChromeRunning()
            => Process.GetProcessesByName("chrome").Length > 0;

        /// <summary>
        /// 指定URLをChromeで開く。
        /// 戻り値: (成功フラグ, 開いたウィンドウのHWND, エラーメッセージ)
        /// </summary>
        public async Task<(bool success, IntPtr hwnd, string error)> OpenUrlAsync(
            string url, string chromePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(chromePath))
                chromePath = FindChromePath();

            if (string.IsNullOrEmpty(chromePath))
                return (false, IntPtr.Zero, "Chrome の実行ファイルが見つかりません。設定画面でパスを指定してください。");

            try
            {
                // 開く前のChromeウィンドウ一覧を記録
                var beforeHwnds = GetChromeWindowHandles();

                var psi = new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = $"--new-window \"{url}\"",
                    UseShellExecute = false,
                };
                Process.Start(psi);

                // 新しいウィンドウが出るまで最大10秒待つ
                IntPtr newHwnd = IntPtr.Zero;
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500, ct);
                    var afterHwnds = GetChromeWindowHandles();
                    var newOnes = afterHwnds.Except(beforeHwnds).ToList();
                    if (newOnes.Count > 0)
                    {
                        newHwnd = newOnes.Last();
                        break;
                    }
                }

                if (newHwnd == IntPtr.Zero)
                    return (false, IntPtr.Zero, "新しいChromeウィンドウを検出できませんでした。");

                lock (_managedWindows)
                    _managedWindows[newHwnd] = url;

                return (true, newHwnd, "");
            }
            catch (Exception ex)
            {
                return (false, IntPtr.Zero, ex.Message);
            }
        }

        /// <summary>
        /// 本アプリが開いたウィンドウを閉じる。
        /// </summary>
        public (bool success, string error) CloseWindow(IntPtr hwnd)
        {
            try
            {
                lock (_managedWindows)
                    _managedWindows.Remove(hwnd);

                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // 現在表示中のChromeメインウィンドウのHWND一覧を返す
        private static List<IntPtr> GetChromeWindowHandles()
        {
            var result = new List<IntPtr>();
            var chromeProcs = Process.GetProcessesByName("chrome")
                                     .Select(p => (uint)p.Id)
                                     .ToHashSet();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (!chromeProcs.Contains(pid)) return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();
                // タイトルが空でないChromeウィンドウだけ対象
                if (!string.IsNullOrEmpty(title))
                    result.Add(hWnd);

                return true;
            }, IntPtr.Zero);

            return result;
        }
    }
}
