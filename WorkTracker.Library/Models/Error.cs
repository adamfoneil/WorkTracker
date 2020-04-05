using AO.DbSchema.Attributes;
using System;

namespace WorkTracker.Library.Models
{
    [Schema(JobTracker.Schema)]
    public class Error
    {
        public long Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [References(typeof(Job))]
        public long JobId { get; set; }

        public string Message { get; set; }        
    }
}
