using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System;
using System.Threading.Tasks;

namespace JobManager.Test
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void SimpleJob()
        {
            using (var cn = LocalDb.GetConnection("JobManager")) { }

            var jobManager = new Library.JobManager(LocalDb.GetConnectionString("JobManager"));
            jobManager.ExecuteAsync("all-jobs", "my-job", async () =>
            {
                Console.WriteLine("this is my job");
                await Task.CompletedTask;
            }).Wait();
        }
    }
}
