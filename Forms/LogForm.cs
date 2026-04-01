using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Models;
using PeriodicAccessTool.Services;

namespace PeriodicAccessTool.Forms
{
    public class LogForm : Form
    {
        private readonly LogService _log;
        private readonly DataManager _data;

        private ListView _listView = null!;
        private DateTimePicker _dtFrom = null!;
        private DateTimePicker _dtTo = null!;
        private ComboBox _cmbStatus = null!;

        public LogForm(LogService log, DataManager data)
        {
            _log = log;
            _data = data;
            InitializeComponent();
            RefreshLogs();
        }

        private void InitializeComponent()
        {
            Text = "実行ログ";
            Size = new System.Drawing.Size(860, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ---- フィルターパネル ----
            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(6),
            };

            filterPanel.Controls.Add(new Label { Text = "開始日:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            _dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-7), Width = 100 };
            filterPanel.Controls.Add(_dtFrom);

            filterPanel.Controls.Add(new Label { Text = " 終了日:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            _dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today, Width = 100 };
            filterPanel.Controls.Add(_dtTo);

            filterPanel.Controls.Add(new Label { Text = " 結果:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            _cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            _cmbStatus.Items.AddRange(new object[] { "全て", "成功のみ", "失敗のみ" });
            _cmbStatus.SelectedIndex = 0;
            filterPanel.Controls.Add(_cmbStatus);

            var btnFilter = new Button { Text = "絞込", Width = 60 };
            btnFilter.Click += (_, _) => RefreshLogs();
            filterPanel.Controls.Add(btnFilter);

            var btnCsv = new Button { Text = "CSV出力", Width = 80 };
            btnCsv.Click += (_, _) => ExportCsv();
            filterPanel.Controls.Add(btnCsv);

            var btnPrune = new Button { Text = "古いログ削除", Width = 100 };
            btnPrune.Click += (_, _) => PruneLogs();
            filterPanel.Controls.Add(btnPrune);

            Controls.Add(filterPanel);

            // ---- ListView ----
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            _listView.Columns.Add("日時", 130);
            _listView.Columns.Add("管理名", 130);
            _listView.Columns.Add("URL", 260);
            _listView.Columns.Add("結果", 80);
            _listView.Columns.Add("Chrome起動", 80);
            _listView.Columns.Add("詳細", 200);
            Controls.Add(_listView);
        }

        private void RefreshLogs()
        {
            bool? successOnly = _cmbStatus.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null
            };

            var logs = _log.GetFiltered(_dtFrom.Value.Date, _dtTo.Value.Date.AddDays(1), successOnly);
            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (var l in logs)
            {
                var item = new ListViewItem(l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(l.EntryName);
                item.SubItems.Add(l.Url);
                item.SubItems.Add(l.Status);
                item.SubItems.Add(l.ChromeLaunched ? "○起動" : "既起動");
                item.SubItems.Add(l.ErrorMessage);
                if (!l.OpenSuccess || !l.CloseSuccess)
                    item.ForeColor = System.Drawing.Color.Red;
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();
        }

        private void ExportCsv()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV ファイル|*.csv",
                FileName = $"実行ログ_{DateTime.Now:yyyyMMdd}.csv",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            bool? successOnly = _cmbStatus.SelectedIndex switch { 1 => true, 2 => false, _ => null };
            var logs = _log.GetFiltered(_dtFrom.Value.Date, _dtTo.Value.Date.AddDays(1), successOnly);

            var sb = new StringBuilder();
            sb.AppendLine("日時,管理名,URL,結果,Chrome起動,詳細");
            foreach (var l in logs)
                sb.AppendLine($"{l.Timestamp:yyyy-MM-dd HH:mm:ss},{Esc(l.EntryName)},{Esc(l.Url)},{l.Status},{(l.ChromeLaunched ? "起動" : "既起動")},{Esc(l.ErrorMessage)}");

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("出力しました。", "CSV出力", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PruneLogs()
        {
            var settings = _data.GetSettings();
            if (MessageBox.Show($"{settings.LogRetentionDays} 日より古いログを削除しますか？",
                "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _log.Prune(settings.LogRetentionDays);
                RefreshLogs();
            }
        }

        private static string Esc(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
