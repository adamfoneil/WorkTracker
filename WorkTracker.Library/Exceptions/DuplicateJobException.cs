using JobManager.Library.Models;
using System;

namespace JobManager.Library.Exceptions
{
    public class DuplicateJobException : Exception
    {        
        public DuplicateJobException(Job job) : base($"The job {job.UserName}:{job.Key} has already run.")
        {
            Job = job;
        }

        public Job Job { get; set; }
    }
}
