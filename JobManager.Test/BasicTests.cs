using JobManager.Library;
using JobManager.Library.Models;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System;
using Dapper.CX.Extensions;
using Dapper.CX.SqlServer.Extensions.Long;
using JobManager.Library.Exceptions;
using Newtonsoft.Json.Linq;

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
                jobId = job.CurrentJob.Id;
            }

            AssertJobExists(jobId);
        }

        [TestMethod]
        public void SimpleFailedJob()
        {
            long jobId = 0;

            using (var job = JobTracker.StartAsync("adamo", GetConnection).Result)
            {
                jobId = job.CurrentJob.Id;
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
                jobId = job.CurrentJob.Id;
                job.SucceededAsync().Wait();
            }

            AssertJobExists(jobId);
        }

        [TestMethod]
        public void RetryJob()
        {
            string key = Guid.NewGuid().ToString();

            // create the first (failed) job
            using (var job = JobTracker.StartUniqueAsync("adamo", key, GetConnection).Result)
            {
                job.FailedAsync("sample failure").Wait();
            }

            // we're allowed to retry a failed job
            using (var job = JobTracker.StartUniqueAsync("adamo", key, GetConnection).Result)
            {
                job.SucceededAsync().Wait();
            }
        }

        [TestMethod]
        public void DuplicateJob()
        {
            var key = Guid.NewGuid().ToString();

            // first job will work (and succeed by default)
            using (var job = JobTracker.StartUniqueAsync("adamo", key, GetConnection).Result)
            {
            }

            // second call should fail with DuplicateJobException
            try
            {
                using (var job = JobTracker.StartUniqueAsync("adamo", key, GetConnection).Result)
                {
                }
            }
            catch (Exception exc)
            {
                Assert.IsTrue(exc.InnerException is DuplicateJobException);
            }
        }

        [TestMethod]
        public void CustomDataSerialization()
        {
            var data = new
            {
                fileName = "sampleFile.pdf",
                date = new DateTime(2020, 4, 4),
                flag = true,
                values = new string[] { "this", "that", "other" }
            };

            using (var job = JobTracker.StartAsync("adamo", GetConnection, data).Result)
            {
                string json = job.ToJson();
                var obj = JObject.Parse(json);
                Assert.IsTrue(obj.ContainsKey("data"));
            }
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
