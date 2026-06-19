using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace payroll.API.Models
{
    public class PayslipModel
    {
        public string EmployeeName { get; set; } = "";
        public string Position { get; set; } = "Staff";
        public string Basis { get; set; } = "";
        public double HourlyRate { get; set; }
        public double BasicPay { get; set; }
        public double OvertimePay { get; set; }
        public double OvertimeHours { get; set; }
        public double PerfectAttendance { get; set; }

        // 🎯 DYNAMIC LABELS ADDED
        public string IncentivesLabel { get; set; } = "Incentives";
        public double Incentives { get; set; }

        public double RestDayPay { get; set; }
        public double AbsenceDeduction { get; set; }
        public double LateMinutes { get; set; }
        public double LateDeduction { get; set; }
        public double UndertimeMinutes { get; set; }
        public double UndertimeDeduction { get; set; }
        public double SSS { get; set; }
        public double PhilHealth { get; set; }
        public double PagIBIG { get; set; }
        public double CashAdvanceDeduction { get; set; }

        // 🎯 DYNAMIC LABELS ADDED
        public string OthersLabel { get; set; } = "Others";
        public double OthersDeduction { get; set; }

        public double Deductions { get; set; }
        public double NetPay { get; set; }
        public string DateGenerated { get; set; } = "";
        public bool IsSent { get; set; }

        public string dtr_logs { get; set; } = "[]";

        [JsonIgnore]
        public List<DailyLog> DtrLogs { get; set; } = new List<DailyLog>();

        [JsonIgnore]
        public double MinusMinutes
        {
            get => UndertimeMinutes;
            set => UndertimeMinutes = value;
        }
        public double Tax { get; set; }
    }
}