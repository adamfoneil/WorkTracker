using Dapper.CX.SqlServer.Extensions.Int;
using Microsoft.Data.SqlClient;
using ModelSync.Library.Models;
using System;
using System.Threading.Tasks;
using WorkTracker.Library.Models;

namespace WorkTracker.Library
{
    public class WorkTracker
    {
        private readonly string _connectionString;

        public WorkTracker(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        internal const string Schema = "worktracker";

        public async Task InitializeAsync()
        {
            await DataModel.CreateTablesAsync(new[]
            {
                typeof(Failed),
                typeof(Succeeded),
                typeof(Working)
            }, GetConnection);
        }

        public async Task StartAsync(string queue, string name)
        {
            using (var cn = GetConnection())
            {
                var working = await GetWorkingAsync(cn, queue, name);
                if (working != null) throw new Exception($"Already working on {name} in queue {queue}");
                await StartInnerAsync(cn, queue, name);
            }
        }

        public async Task DoWork(string queue, string name, Func<Task> work)
        {
            if (await CanStartAsync(queue, name))
            {
                try
                {
                    await work.Invoke();
                    await SucceededAsync(queue, name);
                }
                catch (Exception exc)
                {
                    await FailedAsync(queue, name, exc.Message);
                }
            }                                                                      
        }

        public async Task<bool> CanStartAsync(string queue, string name)
        {
            using (var cn = GetConnection())
            {
                if (!(await IsWorkingAsync(cn, queue, name)))
                {
                    await StartInnerAsync(cn, queue, name);
                    return true;
                }
            }

            return false;
        }

        public async Task SucceededAsync(string queue, string name)
        {
            using (var cn = GetConnection())
            {
                var working = await GetWorkingAsync(cn, queue, name);
                if (working == null) throw new Exception($"Not working on {name} in queue {queue}");

                using (var txn = cn.BeginTransaction())
                {
                    await cn.SaveAsync(new Succeeded() 
                    { 
                        Queue = working.Queue, 
                        Name = working.Name, 
                        Data = working.Data,
                        Started = working.Timestamp
                    }, txn: txn);
                    await cn.DeleteAsync<Working>(working.Id, txn);
                }
            }
        }

        public async Task FailedAsync(string queue, string name, string message)
        {
            using (var cn = GetConnection())
            {
                var working = await GetWorkingAsync(cn, queue, name);
                if (working == null) throw new Exception($"Not working on {name} in queue {queue}");

                using (var txn = cn.BeginTransaction())
                {
                    await cn.SaveAsync(new Failed() 
                    { 
                        Queue = working.Queue, 
                        Name = working.Name, 
                        Started = working.Timestamp, 
                        Data = working.Data,
                        Message = message 
                    }, txn: txn);
                    await cn.DeleteAsync<Working>(working.Id, txn);
                }
            }
        }

        private async Task<Working> GetWorkingAsync(SqlConnection connection, string queue, string name)
        {
            return await connection.GetWhereAsync<Working>(new { queue, name });
        }

        public async Task<bool> IsWorkingAsync(string queue, string name)
        {
            using (var cn = GetConnection())
            {
                return await IsWorkingAsync(cn, queue, name);
            }
        }

        public async Task<bool> IsWorkingAsync(SqlConnection connection, string queue, string name)
        {
            var working = await GetWorkingAsync(connection, queue, name);
            return working != null;
        }

        public async Task<bool> HasSucceededAsync(SqlConnection connection, string queue, string name)
        {
            // todo: make this QueryFirstOrDefault not QuerySingleOrDefault
            var succeeded = await connection.GetWhereAsync<Succeeded>(new { queue, name });
            return succeeded != null;
        }

        private async Task StartInnerAsync(SqlConnection cn, string queue, string name, string data = null)
        {
            await cn.SaveAsync(new Working() 
            {
                Queue = queue,
                Name = name,                 
                Data = data 
            });
        }        
    }
}
