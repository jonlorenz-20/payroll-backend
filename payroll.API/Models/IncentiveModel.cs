using System;

namespace payroll.API.Models
{
    public class IncentiveModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = "";
        public string CutoffPeriod { get; set; } = "";
        public string IncentiveName { get; set; } = "";
        public double Amount { get; set; }
    }
}