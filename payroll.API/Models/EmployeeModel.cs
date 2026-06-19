using System;
using System.Text.Json.Serialization;

namespace payroll
{
    public class EmployeeModel
    {
        public int Id { get; set; }

        public string BiometricId { get; set; } = "";

        public string Name { get; set; } = "";

        public string Department { get; set; } = "";

        [JsonPropertyName("position")]
        public string Position { get; set; } = "";

        public string Basis { get; set; } = "";

        public double Rate { get; set; }

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

        public string ShiftSchedule { get; set; } = "7:00 AM - 4:00 PM";

        [JsonPropertyName("dayOff")]
        public string DayOff { get; set; } = "";

        public double CashAdvanceBalance { get; set; }

        public DateTime DateHired { get; set; } = DateTime.Now;

        public string Email { get; set; } = "";

        // 🎯 IDINAGDAG: Recurring / Fixed Deductions
        public double SssDeduct { get; set; }
        public double PhilhealthDeduct { get; set; }
        public double PagibigDeduct { get; set; }
        public double TaxDeduct { get; set; }
    }
}