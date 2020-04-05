using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System.Linq;
using System.Net;
using WorkTracker.Library;
using WorkTracker.Library.Models;

namespace WorkTracker.Test
{
    /// <summary>
    /// to run this, start an instance of the SampleWebhook project first, without debugger.
    /// Then you can debug or run these tests.
    /// </summary>
    [TestClass]
    public class WebhookTests
    {
        private static SqlConnection GetConnection() => LocalDb.GetConnection("JobTracker");

        [TestMethod]
        public void PostAllEvents()
        {
            var options = new JobTrackerOptions()
            {
                WebhookUrl = "http://localhost:7071/api/JobTrackerEvent"
            };

            using (var job = JobTracker.StartAsync("adamo", GetConnection, options).Result)
            {
                job.SucceededAsync().Wait();

                var events = job.QueryEventsAsync().Result;

                Assert.IsTrue(events.Count() == 2);
                Assert.IsTrue(events.All(e => e.ResponseCode == HttpStatusCode.OK));
                Assert.IsTrue(events.Any(e => e.Status == JobStatus.Working));
                Assert.IsTrue(events.Any(e => e.Status == JobStatus.Succeeded));
            }
        }

        [TestMethod]
        public void PostSucceededEvent()
        {
            var options = new JobTrackerOptions()
            {
                WebhookUrl = "http://localhost:7071/api/JobTrackerEvent",
                WebhookEvents = WebhookEventFlags.Succeeded
            };

            using (var job = JobTracker.StartAsync("adamo", GetConnection, options).Result)
            {
                job.SucceededAsync().Wait();

                var events = job.QueryEventsAsync().Result;

                Assert.IsTrue(events.Count() == 1);
                Assert.IsTrue(events.All(e => e.ResponseCode == HttpStatusCode.OK));                
                Assert.IsTrue(events.All(e => e.Status == JobStatus.Succeeded));
            }

        }
    }
}
