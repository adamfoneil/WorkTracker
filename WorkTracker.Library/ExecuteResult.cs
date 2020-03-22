using JobManager.Library.Models;

namespace JobManager.Library
{
    public class ExecuteResult
    {
        public bool Started { get; set; }
        public Job Job { get; set; }
    }
}
