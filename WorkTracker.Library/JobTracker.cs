using Dapper;
using Dapper.CX.Exceptions;
using Dapper.CX.SqlServer.Extensions.Long;
using WorkTracker.Library.Exceptions;
using WorkTracker.Library.Models;
using Microsoft.Data.SqlClient;
using ModelSync.Library.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WorkTracker.Library
{
    public class JobTracker : IDisposable
    {
        private static HttpClient _client = new HttpClient();
        private static bool _initialized = false;
        private static Func<SqlConnection> _getConnection;

        private JobStatus _statusOnDispose = JobStatus.Succeeded;
        private bool _autoDispose = true;
        private readonly Action<JObject> _updateEventData;
        private readonly WebhookEventFlags _events;
        
        internal const string Schema = "jobs";

        private JobTracker(Job job, WebhookEventFlags webhookEventFlags, Action<JObject> updateEventData = null)
        {
            CurrentJob = job;
            _updateEventData = updateEventData;
            _events = webhookEventFlags;
        }
        
        public Job CurrentJob { get; private set; }

        public static async Task<JobTracker> StartUniqueAsync(string userName, string key, Func<SqlConnection> getConnection, JobTrackerOptions options = null)
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
                    StartTime = DateTime.UtcNow,
                    WebhookUrl = options?.WebhookUrl
                };

                if (options?.Data != null) job.Data = JsonConvert.SerializeObject(options?.Data);

                var events = options?.WebhookEvents ?? WebhookEventFlags.Started | WebhookEventFlags.Failed | WebhookEventFlags.Succeeded;

                try_again:
                
                try
                {
                    await cn.SaveAsync(job);
                    await PostWebhookAsync(cn, job, events);
                }
                catch (CrudException)
                {
                    if (await RetryJobAsync(cn, userName, key, 10))
                    {
                        job.IsRetry = true;
                        goto try_again;
                    }
                    else
                    {
                        var existingJob = await cn.GetWhereAsync<Job>(new { userName, key });
                        if (existingJob != null) throw new DuplicateJobException(existingJob);
                        throw;
                    }                    
                }

                return new JobTracker(job, events, options?.UpdateEventData);
            }
        }

        public static async Task<JobTracker> StartAsync(string userName, Func<SqlConnection> getConnection, JobTrackerOptions options = null)
        {
            return await StartUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, options);
        }

        public static async Task<bool> ExecuteUniqueAsync(string userName, string key, Func<SqlConnection> getConnection, Func<Task> action, JobTrackerOptions options = null)
        {
            try
            {
                using (var job = await StartUniqueAsync(userName, key, getConnection, options))
                {
                    try
                    {
                        await action.Invoke();
                        await job.SucceededAsync();
                        return true;
                    }
                    catch (Exception exc)
                    {
                        await job.FailedAsync(exc);
                        return false;
                    }
                }
            }
            catch (Exception exc)
            {
                if (exc.InnerException is DuplicateJobException) return false;
                throw;
            }
        }

        public string ToJson()
        {
            return ToJsonInner(CurrentJob, _updateEventData);
        }

        private static string ToJsonInner(Job job, Action<JObject> updateEventData = null)
        {
            var obj = JObject.FromObject(job);
            
            if (job.Data != null)
            {
                var data = JObject.Parse(job.Data);
                obj.Add("data", data);
            }
            
            updateEventData?.Invoke(obj);
                        
            return obj.ToString();
        }

        public static async Task<bool> ExecuteAsync(string userName, Func<SqlConnection> getConnection, Func<Task> action, JobTrackerOptions options = null)
        {
            return await ExecuteUniqueAsync(userName, Guid.NewGuid().ToString(), getConnection, action, options);
        }

        private static async Task PostWebhookAsync(SqlConnection cn, Job job, WebhookEventFlags events)
        {
            if (string.IsNullOrEmpty(job.WebhookUrl)) return;

            if (job.Status == JobStatus.Working)
            {
                if ((events & WebhookEventFlags.Started) != WebhookEventFlags.Started) return;
            }

            if (job.Status == JobStatus.Succeeded)
            {
                if ((events & WebhookEventFlags.Succeeded) != WebhookEventFlags.Succeeded) return;
            }

            if (job.Status == JobStatus.Failed)
            {
                if ((events & WebhookEventFlags.Failed) != WebhookEventFlags.Failed) return;
            }

            string json = ToJsonInner(job);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(job.WebhookUrl, content);

            var @event = new Event()
            {
                Timestamp = DateTime.UtcNow,
                JobId = job.Id,
                Status = job.Status,
                Url = job.WebhookUrl,
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
                        JobId = CurrentJob.Id,
                        Message = message,
                        Timestamp = DateTime.UtcNow
                    }, txn: txn);

                    await EndJobAsync(cn, CurrentJob, JobStatus.Failed, _events, txn);
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
                await EndJobAsync(cn, CurrentJob, JobStatus.Succeeded, _events);
            }

            _autoDispose = false;
        }

        public async Task<IEnumerable<Event>> QueryEventsAsync()
        {
            using (var cn = _getConnection.Invoke())
            {
                return await cn.QueryAsync<Event>("SELECT * FROM [jobs].[Event] WHERE [JobId]=@id", new { CurrentJob.Id });
            }
        }

        public async Task<IEnumerable<Error>> QueryErrorsAsync()
        {
            using (var cn = _getConnection.Invoke())
            {
                return await cn.QueryAsync<Error>("SELECT * FROM [jobs].[Error] WHERE [JobId]=@id", new { CurrentJob.Id });
            }
        }

        public async Task<IEnumerable<Retry>> QueryRetriesAsync()
        {
            using (var cn = _getConnection.Invoke())
            {
                return await cn.QueryAsync<Retry>("SELECT * FROM [jobs].[Retry] WHERE [UserName]=@userName AND [Key]=@key", new { CurrentJob.UserName, CurrentJob.Key });
            }
        }

        private static async Task EndJobAsync(SqlConnection cn, Job job, JobStatus status, WebhookEventFlags webhookEvents, IDbTransaction txn = null)
        {
            job.Status = status;
            job.EndTime = DateTime.UtcNow;
            job.Duration = job.EndTime?.Subtract(job.StartTime) ?? TimeSpan.Zero;
            await cn.SaveAsync(job, txn: txn);
            await PostWebhookAsync(cn, job, webhookEvents);
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
                CurrentJob.Status = _statusOnDispose;
                CurrentJob.EndTime = DateTime.UtcNow;
                CurrentJob.Duration = CurrentJob.EndTime.Value.Subtract(CurrentJob.StartTime);
                cn.Update(CurrentJob, model => model.Status, model => model.EndTime, model => model.Duration);
            }
        }
    }
}
