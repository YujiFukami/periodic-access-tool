using System;

namespace PeriodicAccessTool.Models
{
    public class ExecutionLog
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EntryId { get; set; } = "";
        public string EntryName { get; set; } = "";
        public string Url { get; set; } = "";
        public bool ChromeLaunched { get; set; }
        public bool OpenSuccess { get; set; }
        public bool CloseSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";

        public string Status => OpenSuccess
            ? (CloseSuccess ? "成功" : "クローズ失敗")
            : "オープン失敗";
    }
}
