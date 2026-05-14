using System;

namespace payroll
{
    public class EmployeeModel
    {
       
        public int Id { get; set; }

        public string BiometricId { get; set; }

        public string Name { get; set; }

        public string Department { get; set; }

        public string Basis { get; set; }

        public double Rate { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string ShiftSchedule { get; set; } = "7:00 AM - 4:00 PM";

        public double CashAdvanceBalance { get; set; }

        public DateTime DateHired { get; set; } = DateTime.Now;

        public string Email { get; set; }
    }
}