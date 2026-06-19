using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;
using System.Globalization;
using System.Text.Json;

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

        // Helper class para madaling basahin ni Dapper yung bagong column
        private class DtrHistoryDbRow
        {
            public int Id { get; set; }
            public string BiometricId { get; set; } = "";
            public string EmployeeName { get; set; } = "";
            public string CutoffPeriod { get; set; } = "";
            public double DaysWorked { get; set; }
            public double LateMinutes { get; set; }
            public double UndertimeMinutes { get; set; }
            public double OvertimeHours { get; set; }
            public DateTime DateUploaded { get; set; }
            public string DailyLogs { get; set; } = ""; // Dito papasok yung JSON
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetDtrHistory()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            // 🎯 FIX: Idinagdag natin ang daily_logs sa SELECT
            string sql = @"SELECT id AS Id, biometric_id AS BiometricId, employee_name AS EmployeeName, 
                            cutoff_period AS CutoffPeriod, days_worked AS DaysWorked, 
                            late_minutes AS LateMinutes, undertime_minutes AS UndertimeMinutes, 
                            overtime_hours AS OvertimeHours, date_uploaded AS DateUploaded,
                            daily_logs AS DailyLogs
                           FROM dtr_history";

            var rawData = await connection.QueryAsync<DtrHistoryDbRow>(sql);
            var historyList = new List<DtrHistoryModel>();

            foreach (var row in rawData)
            {
                var model = new DtrHistoryModel
                {
                    Id = row.Id,
                    BiometricId = row.BiometricId,
                    EmployeeName = row.EmployeeName,
                    CutoffPeriod = row.CutoffPeriod,
                    DaysWorked = row.DaysWorked,
                    LateMinutes = row.LateMinutes,
                    UndertimeMinutes = row.UndertimeMinutes,
                    OvertimeHours = row.OvertimeHours,
                    DateUploaded = row.DateUploaded,
                    Logs = new List<DailyLog>()
                };

                // 🎯 FIX: I-convert pabalik sa List yung nakasave na text sa DB
                if (!string.IsNullOrEmpty(row.DailyLogs))
                {
                    try
                    {
                        model.Logs = JsonSerializer.Deserialize<List<DailyLog>>(row.DailyLogs, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<DailyLog>();
                    }
                    catch { }
                }

                historyList.Add(model);
            }

            return Ok(historyList);
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

                // 🎯 FIX: Isasave na natin ang dailyLogsForClient sa database!
                var record = new
                {
                    BiometricId = bioId,
                    EmployeeName = empModel?.Name ?? "Unknown",
                    CutoffPeriod = $"{cutoffStart:MMM dd, yyyy} - {cutoffEnd:MMM dd, yyyy}",
                    DaysWorked = totalDays,
                    LateMinutes = totalLate,
                    UndertimeMinutes = totalUT,
                    OvertimeHours = totalOT,
                    DateUploaded = DateTime.Now,
                    DailyLogs = JsonSerializer.Serialize(dailyLogsForClient) // <-- CONVERT TO TEXT (JSON)
                };

                string sqlDelete = "DELETE FROM dtr_history WHERE biometric_id = @BiometricId AND cutoff_period = @CutoffPeriod";
                await connection.ExecuteAsync(sqlDelete, record);

                // 🎯 FIX: Dinagdag ang daily_logs sa INSERT command
                string sqlInsert = @"INSERT INTO dtr_history 
                    (biometric_id, employee_name, cutoff_period, days_worked, late_minutes, undertime_minutes, overtime_hours, date_uploaded, daily_logs) 
                    VALUES (@BiometricId, @EmployeeName, @CutoffPeriod, @DaysWorked, @LateMinutes, @UndertimeMinutes, @OvertimeHours, @DateUploaded, @DailyLogs)";

                await connection.ExecuteAsync(sqlInsert, record);

                // Add to processed history for API response
                processedHistory.Add(new DtrHistoryModel
                {
                    BiometricId = record.BiometricId,
                    EmployeeName = record.EmployeeName,
                    CutoffPeriod = record.CutoffPeriod,
                    DaysWorked = record.DaysWorked,
                    LateMinutes = record.LateMinutes,
                    UndertimeMinutes = record.UndertimeMinutes,
                    OvertimeHours = record.OvertimeHours,
                    DateUploaded = record.DateUploaded,
                    Logs = dailyLogsForClient
                });
            }

            return Ok(new { Message = "Success", Data = processedHistory });
        }

        // =========================================================================================
        // 🎯 OVERTIME APPROVAL WORKFLOW ENDPOINTS
        // =========================================================================================

        [HttpGet("approved-overtimes")]
        public async Task<IActionResult> GetApprovedOvertimes()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                string sql = @"SELECT id AS Id, biometric_id AS BiometricId, employee_name AS EmployeeName, 
                                      date_of_ot AS DateOfOt, time_in AS TimeIn, time_out AS TimeOut, 
                                      approved_hours AS ApprovedHours, remarks AS Remarks 
                               FROM approved_overtimes";
                var overtimes = await connection.QueryAsync<ApprovedOvertimeModel>(sql);
                return Ok(overtimes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching approved OTs: {ex.Message}");
            }
        }

        [HttpPost("approve-overtime")]
        public async Task<IActionResult> ApproveOvertime([FromBody] ApprovedOvertimeModel ot)
        {
            if (ot == null) return BadRequest("Invalid overtime data.");
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                string deleteSql = "DELETE FROM approved_overtimes WHERE biometric_id = @BiometricId AND date_of_ot = @DateOfOt";
                await connection.ExecuteAsync(deleteSql, new { ot.BiometricId, ot.DateOfOt });

                string insertSql = @"INSERT INTO approved_overtimes 
                                     (biometric_id, employee_name, date_of_ot, time_in, time_out, approved_hours, remarks) 
                                     VALUES (@BiometricId, @EmployeeName, @DateOfOt, @TimeIn, @TimeOut, @ApprovedHours, @Remarks)";
                await connection.ExecuteAsync(insertSql, ot);
                return Ok(new { Message = "Overtime successfully approved and saved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving approved OT: {ex.Message}");
            }
        }

        [HttpDelete("decline-overtime/{biometricId}/{dateOfOt}")]
        public async Task<IActionResult> DeclineOvertime(string biometricId, string dateOfOt)
        {
            try
            {
                string decodedDate = Uri.UnescapeDataString(dateOfOt);
                using var connection = new NpgsqlConnection(_connectionString);
                string sql = "DELETE FROM approved_overtimes WHERE biometric_id = @BiometricId AND date_of_ot = @DateOfOt";
                await connection.ExecuteAsync(sql, new { BiometricId = biometricId, DateOfOt = decodedDate });
                return Ok(new { Message = "Overtime successfully declined/removed." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error declining OT: {ex.Message}");
            }
        }
    }
}