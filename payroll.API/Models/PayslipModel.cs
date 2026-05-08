using System.Collections.Generic;

namespace payroll.API.Models
{
    public class PayslipModel
    {
        public string EmployeeName { get; set; }
        public string Basis { get; set; }
        public double HourlyRate { get; set; }
        public double BasicPay { get; set; }
        public double OvertimePay { get; set; }
        public double OvertimeHours { get; set; }
        public double PerfectAttendance { get; set; }
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
        public double OthersDeduction { get; set; }
        public double Deductions { get; set; }
        public double NetPay { get; set; }
        public string DateGenerated { get; set; }

        // Ginawang Nullable para iwas sa "The dtr_logs field is required" error
        public string? dtr_logs { get; set; }

        public List<DailyLog> DtrLogs { get; set; } = new List<DailyLog>();
    }
}