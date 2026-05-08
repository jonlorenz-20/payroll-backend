using System.Collections.Generic;

namespace payroll.API.Models
{
    public class DtrSummary
    {
        public double DaysWorked { get; set; }
        public double Overtime { get; set; }
        public double UndertimeMinutes { get; set; }
        public double LateMinutes { get; set; }
        public List<DailyLog> Logs { get; set; } = new List<DailyLog>();
    }
}