using JobManager.Library;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System;

namespace JobManager.Test
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void SimpleSucceedJob()
        {            
            Func<SqlConnection> getConnection = () => LocalDb.GetConnection("JobTracker");

            using (var job = JobTracker.StartAsync("adamo", getConnection).Result)
            {
                // doesn't matter what's in here
            }
        }

        [TestMethod]
        public void SimpleFailedJob()
        {
            Func<SqlConnection> getConnection = () => LocalDb.GetConnection("JobTracker");

            using (var job = JobTracker.StartAsync("adamo", getConnection).Result)
            {
                job.FailedAsync(new Exception("this is an error")).Wait();
            }
        }
    }
}
