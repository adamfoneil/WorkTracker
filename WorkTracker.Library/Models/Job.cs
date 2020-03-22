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

    [Schema(JobManager.Schema)]
    public class Job
    {
        public int Id { get; set; }

        [MaxLength(255)]
        [Key]
        public string PartitionKey { get; set; }

        [MaxLength(255)]
        [Key]
        public string RowKey { get; set; }

        public JobStatus Status { get; set; }

        public int Attempts { get; set; }

        /// <summary>
        /// any additional data about this job that you want to save
        /// </summary>
        public string Data { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }
    }
}
