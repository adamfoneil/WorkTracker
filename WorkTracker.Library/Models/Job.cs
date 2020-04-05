using AO.DbSchema.Attributes;
using ModelSync.Library.Models;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace WorkTracker.Library.Models
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
        [JsonProperty("id")]
        public long Id { get; set; }

        [MaxLength(50)]
        [Key]
        [JsonProperty("userName")]
        public string UserName { get; set; }

        [MaxLength(255)]
        [Key]
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("status")]
        public JobStatus Status { get; set; }

        /// <summary>
        /// additional json data about job -- ignored by default serialization and added back manually for better event payload formatting
        /// </summary>
        [JsonIgnore]        
        public string Data { get; set; }

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonProperty("duration")]
        public TimeSpan? Duration { get; set; }

        [MaxLength(255)]
        [JsonProperty("webhookUrl")]
        public string WebhookUrl { get; set; }

        [JsonProperty("isRetry")]
        public bool IsRetry { get; set; }        
    }
}
