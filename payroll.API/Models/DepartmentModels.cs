namespace payroll.API.Models
{
    public class DepartmentModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PositionModel
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}