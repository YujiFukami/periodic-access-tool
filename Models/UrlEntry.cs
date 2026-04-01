using System;

namespace PeriodicAccessTool.Models
{
    public class UrlEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public int Interval { get; set; } = 30;
        public IntervalUnit IntervalUnit { get; set; } = IntervalUnit.Minutes;
        public int WaitSeconds { get; set; } = 30;
        public bool Enabled { get; set; } = true;
        // DaysOfWeek[0]=日, [1]=月, ..., [6]=土
        public bool[] DaysOfWeek { get; set; } = new bool[] { true, true, true, true, true, true, true };
        public int StartHour { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public string Note { get; set; } = "";
        public DateTime? LastExecuted { get; set; }
        public string LastResult { get; set; } = "";
        public int SortOrder { get; set; } = 0;

        public DateTime? GetNextScheduled()
        {
            if (!Enabled) return null;

            double intervalMinutes = GetIntervalMinutes();
            DateTime baseTime = LastExecuted.HasValue
                ? LastExecuted.Value.AddMinutes(intervalMinutes)
                : DateTime.Now;

            // 過去なら今から次を計算
            if (baseTime < DateTime.Now)
                baseTime = DateTime.Now;

            // 有効な時間帯・曜日になるまで最大7日間探索
            DateTime candidate = baseTime;
            for (int i = 0; i < 7 * 24 * 60; i++)
            {
                if (IsValidTime(candidate))
                    return candidate;
                candidate = candidate.AddMinutes(1);
            }
            return null;
        }

        private double GetIntervalMinutes() => IntervalUnit switch
        {
            IntervalUnit.Minutes => Interval,
            IntervalUnit.Hours => Interval * 60.0,
            IntervalUnit.Days => Interval * 60.0 * 24.0,
            _ => Interval
        };

        public bool IsValidTime(DateTime time)
        {
            if (!DaysOfWeek[(int)time.DayOfWeek]) return false;
            if (time.Hour < StartHour) return false;
            if (time.Hour > EndHour) return false;
            return true;
        }

        public bool IsDueNow()
        {
            if (!Enabled) return false;
            if (!IsValidTime(DateTime.Now)) return false;

            if (!LastExecuted.HasValue)
                return true;

            double intervalMinutes = GetIntervalMinutes();
            return (DateTime.Now - LastExecuted.Value).TotalMinutes >= intervalMinutes;
        }
    }

    public enum IntervalUnit
    {
        Minutes,
        Hours,
        Days
    }
}
