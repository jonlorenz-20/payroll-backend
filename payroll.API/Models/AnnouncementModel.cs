namespace payroll.API.Models
{
    public class AnnouncementModel
    {
        // Mas maganda kung may Id kapag naka-database na
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Date { get; set; }
    }
}