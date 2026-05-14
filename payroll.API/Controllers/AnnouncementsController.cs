using Microsoft.AspNetCore.Mvc;
using payroll.API.Models;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnnouncementsController : ControllerBase
    {
        
        private static List<AnnouncementModel> _announcements = new List<AnnouncementModel>
        {
            new AnnouncementModel { Id = 1, Title = "Welcome to Sekyur-Link API", Message = "Backend migration successful!", Date = DateTime.Now.ToString("MMM dd, yyyy") }
        };

        
        [HttpGet]
        public IActionResult GetAnnouncements()
        {
            return Ok(_announcements.OrderByDescending(a => a.Id));
        }

        
        [HttpPost]
        public IActionResult PostAnnouncement([FromBody] AnnouncementModel newAnnouncement)
        {
            newAnnouncement.Id = _announcements.Count > 0 ? _announcements.Max(a => a.Id) + 1 : 1;
            _announcements.Add(newAnnouncement);

            return Ok(new { Message = "Broadcast Sent Successfully!" });
        }
    }
}