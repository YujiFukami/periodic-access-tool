using System;
using System.Threading;
using System.Windows.Forms;

namespace PeriodicAccessTool
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            const string mutexName = "PeriodicAccessTool_SingleInstance";

            bool createdNew;
            try
            {
                _mutex = new Mutex(true, mutexName, out createdNew);
            }
            catch (AbandonedMutexException)
            {
                // 前回タスクマネージャー等で強制終了された場合 → 自分がオーナーとして起動してよい
                createdNew = true;
            }

            if (!createdNew)
            {
                MessageBox.Show("定期アクセス支援ツールは既に起動しています。\nタスクトレイをご確認ください。",
                    "起動済み", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();

            Application.ThreadException += (_, e) =>
                MessageBox.Show($"予期しないエラーが発生しました。\n\n{e.Exception.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.Run(new TrayApplicationContext());

            try { _mutex?.ReleaseMutex(); } catch { }
        }
    }
}
