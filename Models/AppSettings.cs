namespace PeriodicAccessTool.Models
{
    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool EnableNotifications { get; set; } = true;
        public int LogRetentionDays { get; set; } = 30;
        public int ChromeDebugPort { get; set; } = 9222;
        public int MaxConcurrentExecutions { get; set; } = 3;
        public int RetryCount { get; set; } = 1;
        public int RetryIntervalSeconds { get; set; } = 30;
        public string ChromePath { get; set; } = "";
    }
}
