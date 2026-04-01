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
            menu.Items.Add("このアプリについて", null, (_, _) => ShowAbout());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("終了", null, (_, _) => ExitApp());
            return menu;
        }

        private void ShowAbout()
        {
            var form = new Form
            {
                Text = "定期アクセス支援ツール について",
                Size = new Size(400, 260),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterScreen,
            };

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(24),
                AutoSize = false,
            };

            // ロゴ（SVGは直接描画できないためテキストで代替）
            var lblLogo = new Label
            {
                Text = "SOFTEX-CELWARE",
                Font = new Font("Arial Black", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 106, 63),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };

            var lblApp = new Label
            {
                Text = "定期アクセス支援ツール  v1.0",
                Font = new Font("メイリオ", 10, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
            };

            var lblDesc = new Label
            {
                Text = "クラウドソーシングサイトへの定期アクセスを自動化するWindows常駐アプリです。",
                AutoSize = false,
                Size = new Size(340, 40),
                Font = new Font("メイリオ", 9),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 12),
            };

            var lnkBlog = new LinkLabel
            {
                Text = "📖 紹介記事・詳細はこちら（SOFTEX-CELWARE）",
                AutoSize = true,
                Font = new Font("メイリオ", 9),
                Margin = new Padding(0, 0, 0, 8),
            };
            lnkBlog.LinkClicked += (_, _) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.softex-celware.com/post/crowdsourcing-auto-login-tool",
                    UseShellExecute = true,
                });

            var lnkGithub = new LinkLabel
            {
                Text = "⭐ GitHub リポジトリ",
                AutoSize = true,
                Font = new Font("メイリオ", 9),
                Margin = new Padding(0, 0, 0, 12),
            };
            lnkGithub.LinkClicked += (_, _) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/YujiFukami/periodic-access-tool",
                    UseShellExecute = true,
                });

            var btnClose = new Button
            {
                Text = "閉じる",
                Width = 90,
                DialogResult = DialogResult.OK,
            };
            btnClose.Click += (_, _) => form.Close();

            panel.Controls.Add(lblLogo);
            panel.Controls.Add(lblApp);
            panel.Controls.Add(lblDesc);
            panel.Controls.Add(lnkBlog);
            panel.Controls.Add(lnkGithub);
            panel.Controls.Add(btnClose);
            form.Controls.Add(panel);
            form.ShowDialog();
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
