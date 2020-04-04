using AO.DbSchema.Attributes;
using System.ComponentModel.DataAnnotations;

namespace JobManager.Library.Models
{
    [Schema(JobTracker.Schema)]
    public class Event
    {
        public long Id { get; set; }

        public long JobId { get; set; }

        public JobStatus Status { get; set; }

        [MaxLength(255)]
        [Required]
        public string Url { get; set; }

        /// <summary>
        /// posted json from the job + any custom data from the job
        /// </summary>
        [Required]
        public string Data { get; set; }

        public int ResponseCode { get; set; }

        public string ResponseContent { get; set; }
    }
}
