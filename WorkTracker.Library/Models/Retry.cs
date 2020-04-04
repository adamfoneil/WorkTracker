﻿using AO.DbSchema.Attributes;
using System;
using System.ComponentModel.DataAnnotations;

namespace JobManager.Library.Models
{
    [Schema(JobTracker.Schema)]
    public class Retry
    {
        public long Id { get; set; }
        
        [MaxLength(50)]
        [Key]
        public string UserName { get; set; }

        [MaxLength(255)]
        [Key]        
        public string Key { get; set; }

        public int Attempts { get; set; }

        public DateTime Timestamp { get; set; }
    }
}