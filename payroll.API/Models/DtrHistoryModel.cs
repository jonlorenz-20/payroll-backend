using System;
using System.Collections.Generic;

namespace payroll.API.Models
{
    public class DtrHistoryModel
    {
        public int Id { get; set; }
        public string BiometricId { get; set; }
        public string EmployeeName { get; set; }
        public string CutoffPeriod { get; set; }
        public double DaysWorked { get; set; }
        public double LateMinutes { get; set; }
        public double UndertimeMinutes { get; set; }
        public double OvertimeHours { get; set; }
        public DateTime DateUploaded { get; set; } = DateTime.Now;

        // Isang beses lang dapat ito naka-declare dito
        public List<DailyLog> Logs { get; set; } = new List<DailyLog>();
    }

}