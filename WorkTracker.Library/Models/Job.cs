using AO.DbSchema.Attributes;
using ModelSync.Library.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace JobManager.Library.Models
{
    public enum JobStatus
    {
        Working,
        Succeeded,
        Failed
    }

    [Schema(JobTracker.Schema)]
    public class Job
    {
        public long Id { get; set; }

        [MaxLength(50)]
        [Key]
        public string UserName { get; set; }

        [MaxLength(255)]
        [Key]
        public string Key { get; set; }

        public JobStatus Status { get; set; }

        /// <summary>
        /// additional json data about 
        /// </summary>
        public string Data { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [MaxLength(255)]
        public string WebhookUrl { get; set; }

        public bool IsRetry { get; set; }
    }
}
