This is a really small library for tracking background jobs in a SQL Server database that works like this:

```csharp
var userName = "adamo";
Func<SqlConnection> getConnection = () => /* whatever method you have that opens a connection */

using (var job = await JobTracker.StartAsync(userName, getConnection))
{
   /*
   do whatever work you need to do in here.
   When the using block exits, the job will be marked successful unless you call FailedAsync somewhere in here
   */
}
```

This will create records in the `jobs.Job` table looking like this:

![img](https://adamosoftware.blob.core.windows.net/images/job-tracker-jobs.png)

Here are all the [model classes](https://github.com/adamosoftware/WorkTracker/tree/master/WorkTracker.Library/Models) JobTracker creates. I use my [ModelSync](https://github.com/adamosoftware/ModelSync) library to [create](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/JobTracker.cs#L237) the tables in your database.

If you want to assure that the same job can't run more than once, use the `StartUniqueAsync` method and pass some sort of key you decide is unique in your application. You'll get an exception if the job has been run before. If the job has failed in the past, it will retry up to 10 times.

```csharp
var userName = "adamo";
var key = "some unique value";
Func<SqlConnection> getConnection = () => /* whatever method you have that opens a connection */

using (var job = await JobTracker.StartUniqueAsync(userName, key, getConnection))
{
   // blah blah blah
}
```

If you need to indicate that a job failed, it would look like this. This will capture the error message and associate it with the job. You can of course catch different exceptions at different points in your job. This is just a bare-minimum example.

```csharp
using (var job = await JobTracker.StartAsync(userName, getConnection))
{
    try
    {
        // do my work here
    }
    catch (Exception exc)
    {
        await job.FailedAsync(exc);
    }
}
```

If you want to log custom data with a job, pass an optional [JobTrackerOptions](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/JobTrackerOptions.cs) argument on the `StartAsync` or `StartUniqueAsync` methods. This will call `JsonConvert.SerializeObject` and store it in the [Job.Data](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/Models/Job.cs#L39) column. This is indeed how you use webhooks with JobTracker, via the [WebhookUrl](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/JobTrackerOptions.cs#L16) property. This is a good way to trigger notifications on the status of background jobs, or to trigger other general-purpose events.

See the [tests](https://github.com/adamosoftware/WorkTracker/blob/master/JobManager.Test/BasicTests.cs) to see it in action. This uses my [SqlServer.LocalDb](https://github.com/adamosoftware/SqlServer.LocalDb) project for easy database connections in test projects. I'm also using [Dapper.CX](https://github.com/adamosoftware/Dapper.CX) for all CRUD operations.
