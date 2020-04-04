using Newtonsoft.Json.Linq;
using System;

namespace JobManager.Library
{
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
        /// implement this to customize the data posted to the webhook
        /// </summary>
        public Func<JObject, JObject> UpdateEventData { get; set; }
    }
}
