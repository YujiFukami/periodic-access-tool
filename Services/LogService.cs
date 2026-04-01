using System;
using System.Collections.Generic;
using System.Linq;
using PeriodicAccessTool.Data;
using PeriodicAccessTool.Models;

namespace PeriodicAccessTool.Services
{
    public class LogService
    {
        private readonly DataManager _data;

        public LogService(DataManager data)
        {
            _data = data;
        }

        public void Write(ExecutionLog log)
        {
            _data.AppendLog(log);
        }

        public List<ExecutionLog> GetAll() => _data.GetLogs();

        public List<ExecutionLog> GetFiltered(DateTime? from, DateTime? to, bool? successOnly)
        {
            var logs = _data.GetLogs();
            if (from.HasValue) logs = logs.Where(l => l.Timestamp >= from.Value).ToList();
            if (to.HasValue) logs = logs.Where(l => l.Timestamp <= to.Value).ToList();
            if (successOnly.HasValue)
            {
                if (successOnly.Value)
                    logs = logs.Where(l => l.OpenSuccess && l.CloseSuccess).ToList();
                else
                    logs = logs.Where(l => !l.OpenSuccess || !l.CloseSuccess).ToList();
            }
            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        public void Prune(int retentionDays)
        {
            _data.PruneLogs(retentionDays);
        }
    }
}
