using System;
using Newtonsoft.Json;

namespace TimeTrackingApp
{
    public class WorkStatusRecord
    {
        [JsonIgnore]
        public string FirebaseKey { get; set; }

        public string UserId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsPlanned { get; set; } // ВАЖНО: различаем план/факт
    }
}