using Dapper.CX.Classes;
using Dapper.CX.SqlServer.Extensions.Int;
using JobManager.Library.Models;
using Microsoft.Data.SqlClient;
using ModelSync.Library.Models;
using System;
using System.Threading.Tasks;

namespace JobManager.Library
{
    public class JobManager
    {
        private readonly string _connectionString;

        public JobManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public event EventHandler<Job> Started;
        public event EventHandler<Job> Succeeded;
        public event EventHandler<Job> Failed;

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        private static bool _initialized = false;

        internal const string Schema = "jobs";

        public async Task InitializeAsync()
        {
            await DataModel.CreateTablesAsync(new[]
            {
                typeof(Job),
                typeof(Error)
            }, GetConnection);

            _initialized = true;
        }

        public async Task<ExecuteResult> ExecuteAsync(string partitionKey, string rowKey, Func<Task> task, string data = null)
        {
            if (!_initialized) await InitializeAsync();

            bool started = false;
            Job job = null;

            using (var cn = GetConnection())
            {                
                job = await cn.GetWhereAsync<Job>(new { partitionKey, rowKey });
                if (job == null)
                {
                    started = true;
                    job = new Job()
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        StartTime = DateTime.UtcNow,
                        Data = data,
                        Status = JobStatus.Working,
                        Attempts = 1
                    };
                    await cn.SaveAsync(job);
                    var ct = new ChangeTracker<Job>(job);

                    try
                    {
                        Started?.Invoke(this, job);
                        await task.Invoke();                        
                        await SucceedAsync(cn, job, ct);
                    }
                    catch (Exception exc)
                    {
                        await FailedAsync(cn, job, ct, exc.Message);
                    }
                }
            }

            return new ExecuteResult()
            {
                Started = started,
                Job = job
            };
        }

        private async Task SucceedAsync(SqlConnection cn, Job job, ChangeTracker<Job> ct)
        {
            job.Status = JobStatus.Succeeded;
            job.EndTime = DateTime.UtcNow;
            await cn.UpdateAsync(job, ct);
            Succeeded?.Invoke(this, job);
        }

        private async Task FailedAsync(SqlConnection cn, Job job, ChangeTracker<Job> ct, string message)
        {
            job.Status = JobStatus.Failed;
            job.EndTime = DateTime.UtcNow;
            
            using (var txn = cn.BeginTransaction())
            {
                await cn.UpdateAsync(job, ct, txn: txn);
                await cn.SaveAsync(new Error()
                {
                    JobId = job.Id,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                }, txn: txn);
            }

            Failed?.Invoke(this, job);
        }
    }
}
