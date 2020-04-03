using AO.DbSchema.Attributes;
using System;

namespace JobManager.Library.Models
{
    [Schema(JobTracker.Schema)]
    public class Error
    {
        public long Id { get; set; }

        [References(typeof(Job))]
        public long JobId { get; set; }

        public string Message { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
