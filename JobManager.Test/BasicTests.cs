using Dapper.CX.SqlServer.Extensions.Long;
using WorkTracker.Library;
using WorkTracker.Library.Exceptions;
using WorkTracker.Library.Models;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SqlServer.LocalDb;
using System;

namespace WorkTracker.Test
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
            const string fileName = "sampleFile.pdf";

            var data = new
            {
                fileName,
                date = new DateTime(2020, 4, 4),
                flag = true,
                values = new string[] { "this", "that", "other" }
            };

            using (var jt = JobTracker.StartAsync("adamo", GetConnection, new JobTrackerOptions()
            {
                Data = data
            }).Result)
            {
                string json = jt.ToJson();
                var obj = JObject.Parse(json);
                Assert.IsTrue(obj.ContainsKey("data"));
                Assert.IsTrue(obj["data"]["fileName"].Value<string>().Equals(fileName));
            }
        }

        [TestMethod]
        public void UpdateCustomEventData()
        {
            Action<JObject> updatePayload = (obj) =>
            {
                obj.Add("greeting", "hello");
                obj.Add("currentTime", DateTime.UtcNow);
            };

            using (var tracker = JobTracker.StartAsync("adamo", GetConnection, new JobTrackerOptions() 
            {
                UpdateEventData = updatePayload
            }).Result)
            {
                string json = tracker.ToJson();
                var obj = JObject.Parse(json);
                Assert.IsTrue(obj.ContainsKey("greeting"));
                Assert.IsTrue(obj.ContainsKey("currentTime"));
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
