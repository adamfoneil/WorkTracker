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

If you want to assure that the same job can't run more than once, use the `StartUniqueAsync` method and pass some sort of key you decide is unique in your application:

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
        await job.FailedAsnc(exc);
    }
}
```

If you want to log custom data with a job, use the optional `data` argument on the `StartAsync` or `StartUniqueAsync` methods. This will call `JsonConvert.SerializeObject` and store it in the [Job.Data](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/Models/Job.cs#L33) column.

This library will (at least try to) create a couple tables in your database [Job](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/Models/Job.cs) and [Error](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/Models/Error.cs). Table creation happens with the [InitializeAsync](https://github.com/adamosoftware/WorkTracker/blob/master/WorkTracker.Library/JobTracker.cs#L72) method.

See the [tests](https://github.com/adamosoftware/WorkTracker/blob/master/JobManager.Test/BasicTests.cs) to see it in action.
