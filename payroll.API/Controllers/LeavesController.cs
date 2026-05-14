using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeavesController : ControllerBase
    {
        private readonly string _connectionString;

        
        public LeavesController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        
        [HttpGet("eligibility")]
        public async Task<IActionResult> GetLeaveEligibility()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            var employees = await connection.QueryAsync<EmployeeModel>("SELECT * FROM employees");

            var eligibilityList = new List<object>();
            DateTime today = DateTime.Now;

            foreach (var emp in employees)
            {
                
                int monthsEmployed = ((today.Year - emp.DateHired.Year) * 12) + today.Month - emp.DateHired.Month;
                if (today.Day < emp.DateHired.Day) monthsEmployed--;
                if (monthsEmployed < 0) monthsEmployed = 0;

                bool isEligible = monthsEmployed >= 6; 

                eligibilityList.Add(new
                {
                    Name = emp.Name,
                    Department = emp.Department,
                    MonthsEmployed = monthsEmployed,
                    IsEligible = isEligible
                });
            }

            return Ok(eligibilityList.OrderByDescending(e => e.GetType().GetProperty("IsEligible").GetValue(e, null)));
        }

        
        [HttpPost]
        public async Task<IActionResult> SubmitLeaveRequest([FromBody] LeaveRequestModel request)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = @"INSERT INTO leave_requests (employee_name, leave_type, date_requested, status) 
                           VALUES (@EmployeeName, @LeaveType, @DateRequested, @Status) RETURNING id;";

            request.Id = await connection.ExecuteScalarAsync<int>(sql, request);
            return Ok(request);
        }

        
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingLeaves()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            var leaves = await connection.QueryAsync<LeaveRequestModel>("SELECT * FROM leave_requests WHERE status = 'Pending'");
            return Ok(leaves);
        }
    }
}