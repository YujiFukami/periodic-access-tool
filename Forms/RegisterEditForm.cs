using System;
using System.Drawing;
using System.Windows.Forms;
using PeriodicAccessTool.Models;

namespace PeriodicAccessTool.Forms
{
    public class RegisterEditForm : Form
    {
        public UrlEntry? Result { get; private set; }
        private readonly UrlEntry? _source;

        // コントロール
        private TextBox _txtName = null!;
        private TextBox _txtUrl = null!;
        private NumericUpDown _numInterval = null!;
        private ComboBox _cmbUnit = null!;
        private NumericUpDown _numWait = null!;
        private CheckBox _chkEnabled = null!;
        private CheckBox[] _chkDays = null!;
        private NumericUpDown _numStartHour = null!;
        private NumericUpDown _numEndHour = null!;
        private TextBox _txtNote = null!;

        public RegisterEditForm(UrlEntry? entry)
        {
            _source = entry;
            InitializeComponent();
            if (entry != null) FillForm(entry);
        }

        private void InitializeComponent()
        {
            Text = _source == null ? "URL 登録" : "URL 編集";
            Size = new Size(480, 480);
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
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            void AddRow(string label, Control ctrl)
            {
                panel.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
                ctrl.Dock = DockStyle.Fill;
                panel.Controls.Add(ctrl, 1, row);
                row++;
            }

            _txtName = new TextBox { MaxLength = 100 };
            AddRow("管理名", _txtName);

            _txtUrl = new TextBox { MaxLength = 2000 };
            AddRow("URL", _txtUrl);

            var intervalPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _numInterval = new NumericUpDown { Minimum = 1, Maximum = 9999, Value = 30, Width = 70 };
            _cmbUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
            _cmbUnit.Items.AddRange(new object[] { "分", "時間", "日" });
            _cmbUnit.SelectedIndex = 0;
            intervalPanel.Controls.Add(_numInterval);
            intervalPanel.Controls.Add(_cmbUnit);
            AddRow("実行間隔", intervalPanel);

            var waitPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _numWait = new NumericUpDown { Minimum = 5, Maximum = 3600, Value = 30, Width = 70 };
            waitPanel.Controls.Add(_numWait);
            waitPanel.Controls.Add(new Label { Text = "秒後に閉じる", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            AddRow("待機秒数", waitPanel);

            _chkEnabled = new CheckBox { Text = "有効", Checked = true };
            AddRow("状態", _chkEnabled);

            // 曜日チェック
            var dayPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            string[] dayNames = { "日", "月", "火", "水", "木", "金", "土" };
            _chkDays = new CheckBox[7];
            for (int i = 0; i < 7; i++)
            {
                _chkDays[i] = new CheckBox { Text = dayNames[i], Checked = true, AutoSize = true };
                dayPanel.Controls.Add(_chkDays[i]);
            }
            AddRow("実行曜日", dayPanel);

            // 時間帯
            var hourPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _numStartHour = new NumericUpDown { Minimum = 0, Maximum = 23, Value = 0, Width = 55 };
            _numEndHour = new NumericUpDown { Minimum = 0, Maximum = 23, Value = 23, Width = 55 };
            hourPanel.Controls.Add(new Label { Text = "開始", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            hourPanel.Controls.Add(_numStartHour);
            hourPanel.Controls.Add(new Label { Text = "時  終了", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            hourPanel.Controls.Add(_numEndHour);
            hourPanel.Controls.Add(new Label { Text = "時", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            AddRow("時間帯", hourPanel);

            _txtNote = new TextBox { MaxLength = 500 };
            AddRow("備考", _txtNote);

            // ボタン
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
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

            panel.RowCount = row;
            Controls.Add(panel);
            Controls.Add(btnPanel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (_, _) =>
            {
                if (!Validate()) return;
                Result = BuildEntry();
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private void FillForm(UrlEntry e)
        {
            _txtName.Text = e.Name;
            _txtUrl.Text = e.Url;
            _numInterval.Value = e.Interval;
            _cmbUnit.SelectedIndex = (int)e.IntervalUnit;
            _numWait.Value = e.WaitSeconds;
            _chkEnabled.Checked = e.Enabled;
            for (int i = 0; i < 7; i++) _chkDays[i].Checked = e.DaysOfWeek[i];
            _numStartHour.Value = e.StartHour;
            _numEndHour.Value = e.EndHour;
            _txtNote.Text = e.Note;
        }

        private new bool Validate()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("管理名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return false;
            }
            if (string.IsNullOrWhiteSpace(_txtUrl.Text))
            {
                MessageBox.Show("URL を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtUrl.Focus();
                return false;
            }
            if (!Uri.TryCreate(_txtUrl.Text.Trim(), UriKind.Absolute, out _))
            {
                MessageBox.Show("URL の形式が正しくありません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtUrl.Focus();
                return false;
            }
            return true;
        }

        private UrlEntry BuildEntry()
        {
            var entry = _source != null
                ? new UrlEntry
                {
                    Id = _source.Id,
                    LastExecuted = _source.LastExecuted,
                    LastResult = _source.LastResult,
                    SortOrder = _source.SortOrder,
                }
                : new UrlEntry();

            entry.Name = _txtName.Text.Trim();
            entry.Url = _txtUrl.Text.Trim();
            entry.Interval = (int)_numInterval.Value;
            entry.IntervalUnit = (IntervalUnit)_cmbUnit.SelectedIndex;
            entry.WaitSeconds = (int)_numWait.Value;
            entry.Enabled = _chkEnabled.Checked;
            entry.DaysOfWeek = new bool[7];
            for (int i = 0; i < 7; i++) entry.DaysOfWeek[i] = _chkDays[i].Checked;
            entry.StartHour = (int)_numStartHour.Value;
            entry.EndHour = (int)_numEndHour.Value;
            entry.Note = _txtNote.Text.Trim();
            return entry;
        }
    }
}
