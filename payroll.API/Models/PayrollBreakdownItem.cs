namespace payroll.API.Models
{
    public class PayrollBreakdownItem
    {
        public string EmployeeName { get; set; }
        public double BaseSalary { get; set; }
        public double Deductions { get; set; }
        public double NetPay { get; set; }
    }
}