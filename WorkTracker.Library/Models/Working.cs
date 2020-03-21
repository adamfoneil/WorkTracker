using AO.DbSchema.Attributes;
using System;
using System.ComponentModel.DataAnnotations;

namespace WorkTracker.Library.Models
{
    [Schema(WorkTracker.Schema)]
    public class Working
    {
        public int Id { get; set; }

        [MaxLength(255)]
        [Key]
        public string Queue { get; set; }

        [MaxLength(255)]
        [Key]
        public string Name { get; set; }

        public string Data { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
