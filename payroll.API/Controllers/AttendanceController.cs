using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;
using System.Globalization;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        private readonly string _connectionString;

        public AttendanceController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        
        [HttpGet("history")]
        public async Task<IActionResult> GetDtrHistory()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = @"SELECT id, biometric_id AS BiometricId, employee_name AS EmployeeName, 
                            cutoff_period AS CutoffPeriod, days_worked AS DaysWorked, 
                            late_minutes AS LateMinutes, undertime_minutes AS UndertimeMinutes, 
                            overtime_hours AS OvertimeHours, date_uploaded AS DateUploaded 
                           FROM dtr_history";
            var history = await connection.QueryAsync<DtrHistoryModel>(sql);
            return Ok(history);
        }

        
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDtr(IFormFile file, [FromQuery] DateTime cutoffStart, [FromQuery] DateTime cutoffEnd)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            using var connection = new NpgsqlConnection(_connectionString);

            
            var empList = (await connection.QueryAsync<EmployeeModel>(
                "SELECT biometric_id AS BiometricId, name AS Name, shift_schedule AS ShiftSchedule FROM employees"
            )).ToList();

            var rawLogs = new List<(string Id, DateTime Time)>();

            
            using (var stream = new StreamReader(file.OpenReadStream()))
            {
                string line;
                while ((line = await stream.ReadLineAsync()) != null)
                {
                    var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && DateTime.TryParse(parts[1] + " " + parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime logTime))
                    {
                        
                        if (logTime >= cutoffStart && logTime <= cutoffEnd.AddDays(1).AddTicks(-1))
                        {
                            rawLogs.Add((parts[0].Trim(), logTime));
                        }
                    }
                }
            }

            var processedHistory = new List<DtrHistoryModel>();
            var groupedByEmp = rawLogs.GroupBy(x => x.Id);

            foreach (var empGroup in groupedByEmp)
            {
                string bioId = empGroup.Key;
                var empModel = empList.FirstOrDefault(e =>
                    string.Equals(e.BiometricId?.Trim(), bioId, StringComparison.OrdinalIgnoreCase));

                string shift = empModel?.ShiftSchedule ?? "7:00 AM - 4:00 PM";
                TimeSpan standardStart = shift.Contains("8:00 AM") ? new TimeSpan(8, 0, 0) : new TimeSpan(7, 0, 0);
                TimeSpan gracePeriod = standardStart.Add(TimeSpan.FromMinutes(5));

                double totalDays = 0, totalOT = 0, totalUT = 0, totalLate = 0;
                var dailyLogsForClient = new List<DailyLog>();

                
                foreach (var dayGroup in empGroup.GroupBy(x => x.Time.Date))
                {
                    var times = dayGroup.Select(x => x.Time).ToList();
                    var tIn = times.Min();
                    var tOut = times.Max();

                    if (tIn != tOut) 
                    {
                        double dailyLate = 0, dailyUT = 0, dailyOT = 0;

                        
                        if (tIn.TimeOfDay > gracePeriod)
                            dailyLate = (tIn.TimeOfDay - gracePeriod).TotalMinutes;

                        
                        TimeSpan shiftStartReference = tIn.TimeOfDay < standardStart ? standardStart : tIn.TimeOfDay;
                        TimeSpan expectedOut = shiftStartReference.Add(TimeSpan.FromHours(9));

                        
                        if (tOut.TimeOfDay < expectedOut)
                            dailyUT = (expectedOut - tOut.TimeOfDay).TotalMinutes;

                        
                        else if (tOut.TimeOfDay >= expectedOut.Add(TimeSpan.FromHours(1)))
                            dailyOT = (tOut.TimeOfDay - expectedOut).TotalHours;

                        totalLate += dailyLate;
                        totalUT += dailyUT;
                        totalOT += dailyOT;

                        double hrsPresent = (tOut - tIn).TotalHours;
                        if (hrsPresent >= 4) totalDays += 1;
                        else if (hrsPresent > 0) totalDays += 0.5;

                        
                        dailyLogsForClient.Add(new DailyLog
                        {
                            Date = tIn.ToString("MMM dd, yyyy"),
                            TimeIn = tIn.ToString("hh:mm tt"),
                            TimeOut = tOut.ToString("hh:mm tt"),
                            Remarks = hrsPresent >= 8 ? "Present" : "Incomplete"
                        });
                    }
                }

                var record = new DtrHistoryModel
                {
                    BiometricId = bioId,
                    EmployeeName = empModel?.Name ?? "Unknown",
                    CutoffPeriod = $"{cutoffStart:MMM dd, yyyy} - {cutoffEnd:MMM dd, yyyy}",
                    DaysWorked = totalDays,
                    LateMinutes = totalLate,
                    UndertimeMinutes = totalUT,
                    OvertimeHours = totalOT,
                    DateUploaded = DateTime.Now,
                    Logs = dailyLogsForClient
                };

                
                string sqlDelete = "DELETE FROM dtr_history WHERE biometric_id = @BiometricId AND cutoff_period = @CutoffPeriod";
                await connection.ExecuteAsync(sqlDelete, record);

                string sqlInsert = @"INSERT INTO dtr_history 
                    (biometric_id, employee_name, cutoff_period, days_worked, late_minutes, undertime_minutes, overtime_hours, date_uploaded) 
                    VALUES (@BiometricId, @EmployeeName, @CutoffPeriod, @DaysWorked, @LateMinutes, @UndertimeMinutes, @OvertimeHours, @DateUploaded)";

                await connection.ExecuteAsync(sqlInsert, record);

                processedHistory.Add(record);
            }

            return Ok(new { Message = "Success", Data = processedHistory });
        }
    }
}