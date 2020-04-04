using Dapper;
using Dapper.CX.SqlServer.Extensions.Long;
using JobManager.Library.Models;
using Microsoft.Data.SqlClient;
using ModelSync.Library.Models;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JobManager.Library
{
    public class JobTracker : IDisposable
    {
        private static HttpClient _client = new HttpClient();
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
        public string WebhookUrl { get; }

        private JobStatus _statusOnDispose = JobStatus.Succeeded;
        private bool _autoDispose = true;
        
        public static Job CurrentJob { get; private set; }

        internal const string Schema = "jobs";

        public static async Task<JobTracker> StartUniqueAsync(string userName, string key, Func<SqlConnection> getConnection, object data = null, string webhookUrl = null)
        {
            _getConnection = getConnection;
            using (var cn = _getConnection.Invoke())
            {
                await InitializeAsync(cn);

                CurrentJob = new Job()
                {
                    UserName = userName,
                    Key = key,
                    Status = JobStatus.Working,
                    StartTime = DateTime.UtcNow,
                    WebhookUrl = webhookUrl
                };

                if (data != null) CurrentJob.Data = JsonConvert.SerializeObject(data);

                try_again:
                
                try
                {
                    await cn.SaveAsync(CurrentJob);
                    await PostWebhookAsync(cn);
                }
                catch
                {
                    if (await RetryJobAsync(cn, userName, key, 10))
                    {
                        CurrentJob.IsRetry = true;
                        goto try_again;
                    }
                    else
                    {
                        throw;
                    }
                }

                return new JobTracker(CurrentJob.Id, userName, key);
            }
        }

        public static async Task<JobTracker> StartAsync(string userName, Func<SqlConnection> getConnection, object data = null)
        {
            return await StartUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, data);
        }

        public static async Task ExecuteUniqueAsync(string userName, string key, Func<SqlConnection> getConnection, Func<Task> action, object data = null, string webhookUrl = null)
        {
            using (var job = await StartUniqueAsync(userName, key, getConnection, data, webhookUrl))
            {
                try
                {
                    await action.Invoke();
                    await job.SucceededAsync();
                }
                catch (Exception exc)
                {
                    await job.FailedAsync(exc);
                }
            }
        }

        public static async Task ExecuteAsync(string userName, Func<SqlConnection> getConnection, Func<Task> action, object data = null, string webhookUrl = null)
        {
            await ExecuteUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, action, data, webhookUrl);
        }

        private static async Task PostWebhookAsync(SqlConnection cn)
        {
            if (string.IsNullOrEmpty(CurrentJob.WebhookUrl)) return;

            string json = JsonConvert.SerializeObject(CurrentJob);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(CurrentJob.WebhookUrl, content);

            var @event = new Event()
            {
                JobId = CurrentJob.Id,
                Status = CurrentJob.Status,
                Url = CurrentJob.WebhookUrl,
                Data = json,
                ResponseCode = response.StatusCode,
                ResponseContent = await response.Content.ReadAsStringAsync()
            };

            await cn.SaveAsync(@event);
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

        public async Task FailedAsync(Exception exception) => await FailedAsync(exception.Message);

        public async Task FailedAsync(string message)
        {            
            using (var cn = _getConnection.Invoke())
            {
                using (var txn = cn.BeginTransaction())
                {
                    await cn.SaveAsync(new Error()
                    {
                        JobId = JobId,
                        Message = message,
                        Timestamp = DateTime.UtcNow
                    }, txn: txn);

                    await EndJobAsync(cn, JobStatus.Failed, txn);
                    txn.Commit();
                }                                
            }

            _autoDispose = false;
        }

        /// <summary>
        /// call this as the last line of your work to avoid the synchronous job update
        /// </summary>        
        public async Task SucceededAsync()
        {
            using (var cn = _getConnection.Invoke())
            {
                await EndJobAsync(cn, JobStatus.Succeeded);                
            }

            _autoDispose = false;
        }

        private static async Task EndJobAsync(SqlConnection cn, JobStatus status, IDbTransaction txn = null)
        {
            CurrentJob.Status = status;
            CurrentJob.EndTime = DateTime.UtcNow;
            await cn.SaveAsync(CurrentJob, txn: txn);
            await PostWebhookAsync(cn);
        }

        private static async Task InitializeAsync(SqlConnection cn)
        {
            if (_initialized) return;

            await DataModel.CreateTablesAsync(new[]
            {
                typeof(Job),
                typeof(Error),
                typeof(Retry),
                typeof(Event)
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
