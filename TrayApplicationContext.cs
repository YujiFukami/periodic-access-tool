using System;
using System.Drawing;
using System.Windows.Forms;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Forms;
using PeriodicAccessTool.Services;

namespace PeriodicAccessTool
{
    /// <summary>
    /// タスクトレイ常駐のアプリケーションコンテキスト。
    /// メイン画面を閉じてもトレイに常駐し続ける。
    /// </summary>
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly DataManager _data;
        private readonly ChromeService _chrome;
        private readonly LogService _log;
        private readonly SchedulerService _scheduler;

        private readonly NotifyIcon _trayIcon;
        private MainForm? _mainForm;

        public TrayApplicationContext()
        {
            _data = new DataManager();
            _chrome = new ChromeService();
            _log = new LogService(_data);
            _scheduler = new SchedulerService(_data, _chrome, _log);

            // デフォルトURLがまだ未登録なら初回追加
            EnsureDefaultEntries();

            // スケジューラ起動
            _scheduler.Start();

            // トレイアイコン作成
            _trayIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
                Text = "定期アクセス支援ツール",
                Visible = true,
                ContextMenuStrip = BuildContextMenu(),
            };
            _trayIcon.DoubleClick += (_, _) => ShowMainForm();
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("メイン画面を開く", null, (_, _) => ShowMainForm());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("終了", null, (_, _) => ExitApp());
            return menu;
        }

        private void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_data, _scheduler, _chrome, _log);
            }
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.BringToFront();
        }

        private void ExitApp()
        {
            _scheduler.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            // Environment.Exit でバックグラウンドスレッドも含め完全終了
            Environment.Exit(0);
        }

        private void EnsureDefaultEntries()
        {
            var entries = _data.GetEntries();
            if (entries.Count > 0) return; // 既に登録あり

            var lancers = new Models.UrlEntry
            {
                Name = "Lancers マイページ",
                Url = "https://www.lancers.jp/mypage",
                Interval = 30,
                IntervalUnit = Models.IntervalUnit.Minutes,
                WaitSeconds = 30,
                Enabled = true,
                DaysOfWeek = new bool[] { true, true, true, true, true, true, true },
                StartHour = 0,
                EndHour = 23,
                Note = "Lancers マイページへ30分ごとにアクセス",
                SortOrder = 0,
            };

            var crowdworks = new Models.UrlEntry
            {
                Name = "CrowdWorks ダッシュボード",
                Url = "https://crowdworks.jp/dashboard",
                Interval = 30,
                IntervalUnit = Models.IntervalUnit.Minutes,
                WaitSeconds = 30,
                Enabled = true,
                DaysOfWeek = new bool[] { true, true, true, true, true, true, true },
                StartHour = 0,
                EndHour = 23,
                Note = "CrowdWorks ダッシュボードへ30分ごとにアクセス",
                SortOrder = 1,
            };

            _data.AddEntry(lancers);
            _data.AddEntry(crowdworks);
        }
    }
}
