using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Models;
using PeriodicAccessTool.Services;

namespace PeriodicAccessTool.Forms
{
    public class MainForm : Form
    {
        private readonly DataManager _data;
        private readonly SchedulerService _scheduler;
        private readonly ChromeService _chrome;
        private readonly LogService _log;

        private ListView _listView = null!;
        private Label _statusLabel = null!;
        private System.Windows.Forms.Timer _refreshTimer = null!;

        public MainForm(DataManager data, SchedulerService scheduler, ChromeService chrome, LogService log)
        {
            _data = data;
            _scheduler = scheduler;
            _chrome = chrome;
            _log = log;

            _scheduler.StatusMessage += (_, msg) =>
                BeginInvoke(() => _statusLabel.Text = msg);

            _scheduler.ExecutionFailed += (_, consecutive) =>
            {
                var settings = _data.GetSettings();
                if (settings.EnableNotifications)
                    BeginInvoke(() =>
                        new NotifyIcon { Visible = true, BalloonTipTitle = "定期アクセス支援ツール",
                            BalloonTipText = "実行に失敗しました。ログを確認してください。",
                            BalloonTipIcon = ToolTipIcon.Warning,
                            Icon = SystemIcons.Application }
                        .ShowBalloonTip(5000));
            };

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "定期アクセス支援ツール";
            Size = new Size(900, 540);
            MinimumSize = new Size(700, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ---- ツールバー ----
            var toolbar = new ToolStrip { Dock = DockStyle.Top };
            var btnAdd = new ToolStripButton("追加") { ToolTipText = "新しいURLを登録" };
            var btnEdit = new ToolStripButton("編集") { ToolTipText = "選択中の項目を編集" };
            var btnDelete = new ToolStripButton("削除") { ToolTipText = "選択中の項目を削除" };
            var btnRunNow = new ToolStripButton("今すぐ実行") { ToolTipText = "選択中の項目を今すぐ実行" };
            var btnToggle = new ToolStripButton("有効/無効") { ToolTipText = "選択中の項目の有効/無効を切替" };
            var sep1 = new ToolStripSeparator();
            var btnLog = new ToolStripButton("ログ") { ToolTipText = "実行ログを表示" };
            var btnSettings = new ToolStripButton("設定") { ToolTipText = "アプリ設定" };

            toolbar.Items.AddRange(new ToolStripItem[]
                { btnAdd, btnEdit, btnDelete, new ToolStripSeparator(), btnRunNow, btnToggle, sep1, btnLog, btnSettings });
            Controls.Add(toolbar);

            // ---- ListView ----
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
            };
            _listView.Columns.Add("管理名", 160);
            _listView.Columns.Add("URL", 260);
            _listView.Columns.Add("間隔", 80);
            _listView.Columns.Add("待機(秒)", 70);
            _listView.Columns.Add("有効", 50);
            _listView.Columns.Add("次回予定", 130);
            _listView.Columns.Add("最終実行", 130);
            _listView.Columns.Add("最終結果", 80);
            Controls.Add(_listView);

            // ---- ステータスバー ----
            var statusStrip = new StatusStrip();
            _statusLabel = new Label
            {
                Text = "待機中",
                AutoSize = true,
                Padding = new Padding(2, 3, 0, 0),
            };
            var tssl = new ToolStripStatusLabel("待機中") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(tssl);
            _scheduler.StatusMessage += (_, msg) => BeginInvoke(() => tssl.Text = msg);
            Controls.Add(statusStrip);

            // ---- タイマー ----
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
            _refreshTimer.Tick += (_, _) => RefreshList();
            _refreshTimer.Start();

            // ---- イベント ----
            btnAdd.Click += (_, _) => AddEntry();
            btnEdit.Click += (_, _) => EditEntry();
            btnDelete.Click += (_, _) => DeleteEntry();
            btnRunNow.Click += (_, _) => RunNow();
            btnToggle.Click += (_, _) => ToggleEnabled();
            btnLog.Click += (_, _) => new LogForm(_log, _data).ShowDialog(this);
            btnSettings.Click += (_, _) => { new SettingsForm(_data).ShowDialog(this); };

            _listView.DoubleClick += (_, _) => EditEntry();

            FormClosing += (_, e) =>
            {
                // ×ボタンはトレイ常駐（終了しない）
                e.Cancel = true;
                Hide();
            };

            RefreshList();
        }

        public void RefreshList()
        {
            var entries = _data.GetEntries().OrderBy(e => e.SortOrder).ToList();
            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (var e in entries)
            {
                var next = e.GetNextScheduled();
                var item = new ListViewItem(e.Name) { Tag = e.Id };
                item.SubItems.Add(e.Url);
                item.SubItems.Add($"{e.Interval} {UnitLabel(e.IntervalUnit)}");
                item.SubItems.Add(e.WaitSeconds.ToString());
                item.SubItems.Add(e.Enabled ? "○" : "－");
                item.SubItems.Add(next.HasValue ? next.Value.ToString("MM/dd HH:mm") : "－");
                item.SubItems.Add(e.LastExecuted.HasValue ? e.LastExecuted.Value.ToString("MM/dd HH:mm") : "－");
                item.SubItems.Add(e.LastResult);
                if (!e.Enabled) item.ForeColor = Color.Gray;
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();
        }

        private string UnitLabel(IntervalUnit u) => u switch
        {
            IntervalUnit.Minutes => "分",
            IntervalUnit.Hours => "時間",
            IntervalUnit.Days => "日",
            _ => ""
        };

        private UrlEntry? GetSelectedEntry()
        {
            if (_listView.SelectedItems.Count == 0) return null;
            var id = _listView.SelectedItems[0].Tag as string;
            return _data.GetEntries().FirstOrDefault(e => e.Id == id);
        }

        private void AddEntry()
        {
            var form = new RegisterEditForm(null);
            if (form.ShowDialog(this) == DialogResult.OK && form.Result != null)
            {
                _data.AddEntry(form.Result);
                RefreshList();
            }
        }

        private void EditEntry()
        {
            var entry = GetSelectedEntry();
            if (entry == null) { MessageBox.Show("編集する項目を選択してください。"); return; }
            var form = new RegisterEditForm(entry);
            if (form.ShowDialog(this) == DialogResult.OK && form.Result != null)
            {
                _data.UpdateEntry(form.Result);
                RefreshList();
            }
        }

        private void DeleteEntry()
        {
            var entry = GetSelectedEntry();
            if (entry == null) { MessageBox.Show("削除する項目を選択してください。"); return; }
            if (MessageBox.Show($"「{entry.Name}」を削除しますか？", "確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _data.DeleteEntry(entry.Id);
                RefreshList();
            }
        }

        private void RunNow()
        {
            var entry = GetSelectedEntry();
            if (entry == null) { MessageBox.Show("実行する項目を選択してください。"); return; }
            var settings = _data.GetSettings();
            _ = _scheduler.ExecuteEntryAsync(entry, settings);
        }

        private void ToggleEnabled()
        {
            var entry = GetSelectedEntry();
            if (entry == null) { MessageBox.Show("項目を選択してください。"); return; }
            entry.Enabled = !entry.Enabled;
            _data.UpdateEntry(entry);
            RefreshList();
        }
    }
}
