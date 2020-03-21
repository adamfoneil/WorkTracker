using AO.DbSchema.Attributes;
using System;
using System.ComponentModel.DataAnnotations;

namespace WorkTracker.Library.Models
{
    [Schema(WorkTracker.Schema)]
    public class Succeeded
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string Queue { get; set; }

        [MaxLength(255)]
        public string Name { get; set; }

        public string Data { get; set; }

        public DateTime Started { get; set; }

        public DateTime Finished { get; set; } = DateTime.UtcNow;
    }
}
