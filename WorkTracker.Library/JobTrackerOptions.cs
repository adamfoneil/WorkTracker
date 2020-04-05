using Newtonsoft.Json.Linq;
using System;

namespace WorkTracker.Library
{
    [Flags]
    public enum WebhookEventFlags
    {
        Started = 1,
        Succeeded = 2,
        Failed = 4        
    }

    public class JobTrackerOptions
    {
        /// <summary>
        /// custom data to save with a job
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// post job data to this URL when a job starts, succeeds, or fails
        /// </summary>
        public string WebhookUrl { get; set; }

        /// <summary>
        /// when should I post to webhook?
        /// </summary>
        public WebhookEventFlags WebhookEvents { get; set; } = WebhookEventFlags.Started | WebhookEventFlags.Failed | WebhookEventFlags.Succeeded;

        /// <summary>
        /// implement this to customize the data posted to the webhook
        /// </summary>
        public Action<JObject> UpdateEventData { get; set; }
    }
}
