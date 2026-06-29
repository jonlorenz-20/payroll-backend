using System;

namespace payroll.API.Models
{
    public class SalaryReportModel
    {
        public string CutoffPeriod { get; set; } = "";
        public int EmployeeCount { get; set; }
        public double TotalBaseSalary { get; set; }
        public double TotalIncentives { get; set; }
        public double TotalDeductions { get; set; }
        public double TotalNetPay { get; set; }
    }
}