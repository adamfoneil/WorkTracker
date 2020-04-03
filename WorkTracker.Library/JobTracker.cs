using Dapper.CX.SqlServer.Extensions.Long;
using JobManager.Library.Models;
using Microsoft.Data.SqlClient;
using ModelSync.Library.Models;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace JobManager.Library
{
    public class JobTracker : IDisposable
    {
        private static bool _initialized = false;
        private static Func<SqlConnection> _getConnection;

        private JobTracker(long jobId, string userName, string key)
        {
            JobId = jobId;
            UserName = userName;
            Key = key;
        }

        public long JobId { get; }
        public string UserName { get; }
        public string Key { get; }

        private JobStatus _statusOnDispose = JobStatus.Succeeded;

        internal const string Schema = "jobs";

        public static async Task<JobTracker> StartUniqueAsync(string userName, string key, Func<SqlConnection> getConnection, object data = null)
        {
            _getConnection = getConnection;
            using (var cn = _getConnection.Invoke())
            {
                await InitializeAsync(cn);

                var job = new Job()
                {
                    UserName = userName,
                    Key = key,
                    Status = JobStatus.Working,
                    StartTime = DateTime.UtcNow
                };

                if (data != null) job.Data = JsonConvert.SerializeObject(data);

                var jobId = await cn.SaveAsync(job);
                return new JobTracker(jobId, userName, key);
            }
        }

        public static async Task<JobTracker> StartAsync(string userName, Func<SqlConnection> getConnection, object data = null)
        {
            return await StartUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, data);
        }

        public async Task FailedAsync(Exception exception)
        {
            _statusOnDispose = JobStatus.Failed;
            using (var cn = _getConnection.Invoke())
            {
                await cn.SaveAsync(new Error()
                {
                    JobId = JobId,
                    Message = exception.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private static async Task InitializeAsync(SqlConnection cn)
        {
            if (_initialized) return;

            await DataModel.CreateTablesAsync(new[]
            {
                typeof(Job),
                typeof(Error)
            }, cn);

            _initialized = true;
        }

        public void Dispose()
        {
            using (var cn = _getConnection.Invoke())
            {                
                cn.Update(
                    new Job() { Status = _statusOnDispose, EndTime = DateTime.UtcNow, Id = JobId }, 
                    model => model.Status, model => model.EndTime);
                
            }
        }
    }
}
