using AO.DbSchema.Attributes;
using System;

namespace JobManager.Library.Models
{
    [Schema(JobManager.Schema)]
    public class Error
    {
        public int Id { get; set; }

        [References(typeof(Job))]
        public int JobId { get; set; }

        public string Message { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
