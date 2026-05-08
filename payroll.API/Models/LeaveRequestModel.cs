namespace payroll.API.Models
{
    public class LeaveRequestModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; }
        public string LeaveType { get; set; }
        public string DateRequested { get; set; }
        public string Status { get; set; } = "Pending";
    }
}