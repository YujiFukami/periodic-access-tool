using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Models;

namespace PeriodicAccessTool.Services
{
    public class SchedulerService
    {
        private readonly DataManager _data;
        private readonly ChromeService _chrome;
        private readonly LogService _log;

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;

        private readonly ConcurrentDictionary<string, byte> _running = new();

        public event EventHandler<string>? StatusMessage;
        public event EventHandler<bool>? ExecutionFailed;

        public bool IsRunning => _monitorTask != null && !_monitorTask.IsCompleted;

        public SchedulerService(DataManager data, ChromeService chrome, LogService log)
        {
            _data = data;
            _chrome = chrome;
            _log = log;
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _monitorTask?.Wait(5000);
            _monitorTask = null;
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var settings = _data.GetSettings();
                    var entries = _data.GetEntries();
                    int maxConcurrent = settings.MaxConcurrentExecutions;

                    foreach (var entry in entries)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!entry.Enabled) continue;
                        if (_running.Count >= maxConcurrent) break;
                        if (_running.ContainsKey(entry.Id)) continue;
                        if (!entry.IsDueNow()) continue;

                        _ = ExecuteEntryAsync(entry, settings, ct);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    StatusMessage?.Invoke(this, $"監視ループエラー: {ex.Message}");
                }

                await Task.Delay(30_000, ct).ContinueWith(_ => { });
            }
        }

        public async Task ExecuteEntryAsync(UrlEntry entry, AppSettings? settings = null, CancellationToken ct = default)
        {
            if (!_running.TryAdd(entry.Id, 0)) return;

            settings ??= _data.GetSettings();
            var log = new ExecutionLog
            {
                EntryId = entry.Id,
                EntryName = entry.Name,
                Url = entry.Url,
            };

            try
            {
                StatusMessage?.Invoke(this, $"実行開始: {entry.Name}");

                log.ChromeLaunched = !_chrome.IsChromeRunning();

                // --- URLを開く（リトライあり） ---
                (bool success, IntPtr hwnd, string error) openResult = (false, IntPtr.Zero, "");

                for (int attempt = 0; attempt <= settings.RetryCount; attempt++)
                {
                    if (ct.IsCancellationRequested) return;
                    openResult = await _chrome.OpenUrlAsync(entry.Url, settings.ChromePath, ct);
                    if (openResult.success) break;
                    if (attempt < settings.RetryCount)
                        await Task.Delay(settings.RetryIntervalSeconds * 1000, ct);
                }

                log.OpenSuccess = openResult.success;
                log.ErrorMessage = openResult.error;

                if (!openResult.success)
                {
                    StatusMessage?.Invoke(this, $"オープン失敗: {entry.Name} - {openResult.error}");
                    ExecutionFailed?.Invoke(this, true);
                    return;
                }

                // --- 待機秒数後にウィンドウを閉じる ---
                int waitMs = Math.Max(entry.WaitSeconds, 5) * 1000;
                await Task.Delay(waitMs, ct);

                if (!ct.IsCancellationRequested)
                {
                    var closeResult = _chrome.CloseWindow(openResult.hwnd);
                    log.CloseSuccess = closeResult.success;
                    if (!closeResult.success)
                    {
                        log.ErrorMessage = closeResult.error;
                        StatusMessage?.Invoke(this, $"クローズ失敗: {entry.Name} - {closeResult.error}");
                    }
                    else
                    {
                        StatusMessage?.Invoke(this, $"完了: {entry.Name}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                log.ErrorMessage = "キャンセルされました";
            }
            catch (Exception ex)
            {
                log.ErrorMessage = ex.Message;
                ExecutionFailed?.Invoke(this, true);
            }
            finally
            {
                entry.LastExecuted = DateTime.Now;
                entry.LastResult = log.Status;
                _data.UpdateEntry(entry);
                _log.Write(log);
                _running.TryRemove(entry.Id, out _);
            }
        }
    }
}
