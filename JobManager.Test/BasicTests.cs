using JobManager.Library;
using JobManager.Library.Models;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System;
using Dapper.CX.Extensions;
using Dapper.CX.SqlServer.Extensions.Long;

namespace JobManager.Test
{
    [TestClass]
    public class BasicTests
    {
        private static SqlConnection GetConnection() => LocalDb.GetConnection("JobTracker");

        [TestMethod]
        public void SimpleSucceedJob()
        {
            long jobId = 0;

            using (var job = JobTracker.StartAsync("adamo", GetConnection).Result)
            {
                // doesn't matter what's in here
                jobId = job.JobId;
            }

            AssertJobExists(jobId);
        }

        [TestMethod]
        public void SimpleFailedJob()
        {
            long jobId = 0;

            using (var job = JobTracker.StartAsync("adamo", GetConnection).Result)
            {
                jobId = job.JobId;
                job.FailedAsync(new Exception("this is an error")).Wait();
            }

            AssertJobExists(jobId);

            using (var cn = GetConnection())
            {
                Assert.IsTrue(cn.ExistsWhereAsync<Error>(new { jobId }).Result);
            }
        }

        [TestMethod]
        public void ExplicitSucceedJob()
        {
            long jobId = 0;

            using (var job = JobTracker.StartAsync("adamo", GetConnection).Result)
            {
                jobId = job.JobId;
                job.SucceededAsync().Wait();
            }

            AssertJobExists(jobId);
        }

        private static void AssertJobExists(long jobId)
        {
            using (var cn = GetConnection())
            {
                Assert.IsTrue(cn.ExistsAsync<Job>(jobId).Result);
            }
        }
    }
}
