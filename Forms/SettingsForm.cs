using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Models;

namespace PeriodicAccessTool.Forms
{
    public class SettingsForm : Form
    {
        private readonly DataManager _data;

        private CheckBox _chkStartup = null!;
        private CheckBox _chkNotify = null!;
        private NumericUpDown _numLogDays = null!;
        private TextBox _txtChromePath = null!;
        private NumericUpDown _numMaxConcurrent = null!;
        private NumericUpDown _numRetryCount = null!;
        private NumericUpDown _numRetryInterval = null!;

        public SettingsForm(DataManager data)
        {
            _data = data;
            InitializeComponent();
            FillForm(_data.GetSettings());
        }

        private void InitializeComponent()
        {
            Text = "設定";
            Size = new Size(480, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
                AutoSize = true,
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            void AddRow(string label, Control ctrl)
            {
                panel.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
                ctrl.Dock = DockStyle.Fill;
                panel.Controls.Add(ctrl, 1, row);
                row++;
            }

            _chkStartup = new CheckBox { Text = "Windows起動時に自動起動" };
            AddRow("自動起動", _chkStartup);

            _chkNotify = new CheckBox { Text = "実行失敗時に通知" };
            AddRow("通知", _chkNotify);

            _numLogDays = new NumericUpDown { Minimum = 1, Maximum = 3650, Value = 30 };
            AddRow("ログ保存期間(日)", _numLogDays);

            var chromePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _txtChromePath = new TextBox { Width = 250 };
            var btnBrowse = new Button { Text = "...", Width = 30 };
            btnBrowse.Click += (_, _) =>
            {
                using var dlg = new OpenFileDialog { Filter = "chrome.exe|chrome.exe|全て|*.*", Title = "chrome.exe を選択" };
                if (dlg.ShowDialog() == DialogResult.OK) _txtChromePath.Text = dlg.FileName;
            };
            chromePanel.Controls.Add(_txtChromePath);
            chromePanel.Controls.Add(btnBrowse);
            AddRow("Chrome パス", chromePanel);

            _numMaxConcurrent = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 3 };
            AddRow("最大同時実行数", _numMaxConcurrent);

            _numRetryCount = new NumericUpDown { Minimum = 0, Maximum = 5, Value = 1 };
            AddRow("リトライ回数", _numRetryCount);

            _numRetryInterval = new NumericUpDown { Minimum = 5, Maximum = 300, Value = 30 };
            AddRow("リトライ間隔(秒)", _numRetryInterval);

            // データフォルダを開くボタン
            var btnOpenDir = new Button { Text = "データフォルダを開く", Dock = DockStyle.Fill };
            btnOpenDir.Click += (_, _) =>
            {
                if (Directory.Exists(_data.DataDir))
                    System.Diagnostics.Process.Start("explorer.exe", _data.DataDir);
            };
            panel.Controls.Add(new Label(), 0, row);
            panel.Controls.Add(btnOpenDir, 1, row);
            row++;

            panel.RowCount = row;
            Controls.Add(panel);

            // ---- ボタン ----
            var btnOk = new Button { Text = "保存", DialogResult = DialogResult.OK, Width = 80 };
            var btnCancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Width = 80 };
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                AutoSize = true,
            };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOk);
            Controls.Add(btnPanel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (_, _) => SaveSettings();
        }

        private void FillForm(AppSettings s)
        {
            _chkStartup.Checked = s.StartWithWindows;
            _chkNotify.Checked = s.EnableNotifications;
            _numLogDays.Value = s.LogRetentionDays;
            _txtChromePath.Text = s.ChromePath;
            _numMaxConcurrent.Value = s.MaxConcurrentExecutions;
            _numRetryCount.Value = s.RetryCount;
            _numRetryInterval.Value = s.RetryIntervalSeconds;
        }

        private void SaveSettings()
        {
            var s = new AppSettings
            {
                StartWithWindows = _chkStartup.Checked,
                EnableNotifications = _chkNotify.Checked,
                LogRetentionDays = (int)_numLogDays.Value,
                ChromePath = _txtChromePath.Text.Trim(),
                MaxConcurrentExecutions = (int)_numMaxConcurrent.Value,
                RetryCount = (int)_numRetryCount.Value,
                RetryIntervalSeconds = (int)_numRetryInterval.Value,
            };
            _data.SaveSettings(s);

            // スタートアップ登録/解除
            SetStartup(s.StartWithWindows);
        }

        private static void SetStartup(bool enable)
        {
            const string appName = "定期アクセス支援ツール";
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location
                    .Replace(".dll", ".exe");
                key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
        }
    }
}
