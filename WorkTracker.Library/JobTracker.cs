using Dapper;
using Dapper.CX.Exceptions;
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
        private bool _autoDispose = true;

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

                try_again:

                long jobId = 0;
                try
                {
                    
                    jobId = await cn.SaveAsync(job);
                }
                catch
                {
                    if (await RetryJobAsync(cn, userName, key, 10))
                    {
                        goto try_again;
                    }
                    else
                    {
                        throw;
                    }
                }

                return new JobTracker(jobId, userName, key);
            }
        }

        /// <summary>
        /// if a job has failed in the past, we may retry it a certain number of times
        /// </summary>
        private static async Task<bool> RetryJobAsync(SqlConnection cn, string userName, string key, int maxAttempts)
        {
            try
            {
                var job = await cn.GetWhereAsync<Job>(new { userName, key });
                var retry = await cn.GetWhereAsync<Retry>(new { userName, key }) ?? new Retry() { UserName = userName, Key = key };
                if (job.Status == JobStatus.Failed && retry.Attempts <= maxAttempts)
                {
                    retry.Attempts++;
                    retry.Timestamp = DateTime.UtcNow;

                    using (var txn = cn.BeginTransaction())
                    {
                        await cn.ExecuteAsync("DELETE [jobs].[Error] WHERE [JobId]=@jobId", new { jobId = job.Id }, txn);
                        await cn.DeleteAsync<Job>(job.Id, txn);
                        await cn.SaveAsync(retry, txn: txn);
                        txn.Commit();
                    }
                    
                    return true;
                }

                return false;
            }
            catch 
            {
                return false;
            }
            
        }

        public static async Task<JobTracker> StartAsync(string userName, Func<SqlConnection> getConnection, object data = null)
        {
            return await StartUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, data);
        }

        public async Task FailedAsync(Exception exception) => await FailedAsync(exception.Message);
        
        public async Task FailedAsync(string message)
        {
            _statusOnDispose = JobStatus.Failed;
            using (var cn = _getConnection.Invoke())
            {
                await cn.SaveAsync(new Error()
                {
                    JobId = JobId,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// call this as the last line of your work to avoid the synchronous job update
        /// </summary>        
        public async Task SucceededAsync()
        {
            using (var cn = _getConnection.Invoke())
            {
                await cn.UpdateAsync(
                    new Job() { Status = _statusOnDispose, EndTime = DateTime.UtcNow, Id = JobId },
                    model => model.Status, model => model.EndTime);
            }

            _autoDispose = false;
        }

        private static async Task InitializeAsync(SqlConnection cn)
        {
            if (_initialized) return;

            await DataModel.CreateTablesAsync(new[]
            {
                typeof(Job),
                typeof(Error),
                typeof(Retry)
            }, cn);

            _initialized = true;
        }

        public void Dispose()
        {
            if (!_autoDispose) return;

            using (var cn = _getConnection.Invoke())
            {                
                cn.Update(
                    new Job() { Status = _statusOnDispose, EndTime = DateTime.UtcNow, Id = JobId }, 
                    model => model.Status, model => model.EndTime);                
            }
        }
    }
}
