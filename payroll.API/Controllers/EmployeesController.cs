using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly string _connectionString;

        public EmployeesController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        // 1. GET ALL EMPLOYEES
        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                string sql = @"SELECT 
                                id AS Id, 
                                biometric_id AS BiometricId, 
                                name AS Name, 
                                email AS Email, 
                                department AS Department, 
                                position AS Position, 
                                basis AS Basis, 
                                rate AS Rate, 
                                username AS Username, 
                                password AS Password, 
                                shift_schedule AS ShiftSchedule, 
                                day_off AS DayOff, 
                                cash_advance_balance AS CashAdvanceBalance, 
                                date_hired AS DateHired,
                                sss_deduct AS SssDeduct,
                                philhealth_deduct AS PhilhealthDeduct,
                                pagibig_deduct AS PagibigDeduct,
                                tax_deduct AS TaxDeduct
                               FROM employees";

                var employees = await connection.QueryAsync<EmployeeModel>(sql);
                return Ok(employees.OrderBy(e => e.Name));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database Error: {ex.Message}");
            }
        }

        // 2. SAVE NEW EMPLOYEE
        [HttpPost]
        public async Task<IActionResult> SaveEmployee([FromBody] EmployeeModel emp)
        {
            if (emp == null) return BadRequest("Employee data is null.");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                string sql = @"
                    INSERT INTO employees 
                    (biometric_id, name, email, department, position, basis, rate, username, password, shift_schedule, day_off, cash_advance_balance, date_hired, sss_deduct, philhealth_deduct, pagibig_deduct, tax_deduct) 
                    VALUES 
                    (@BiometricId, @Name, @Email, @Department, @Position, @Basis, @Rate, @Username, @Password, @ShiftSchedule, @DayOff, @CashAdvanceBalance, @DateHired, @SssDeduct, @PhilhealthDeduct, @PagibigDeduct, @TaxDeduct);";

                await connection.ExecuteAsync(sql, emp);
                return Ok(new { Message = "Employee saved successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save Error: {ex.Message}");
                return StatusCode(500, $"Error saving employee: {ex.Message}");
            }
        }

        // 3. UPDATE EMPLOYEE
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] EmployeeModel emp)
        {
            if (emp == null) return BadRequest("Employee data is null.");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                string sql = @"
                    UPDATE employees 
                    SET 
                        biometric_id = @BiometricId,
                        name = @Name, 
                        email = @Email, 
                        department = @Department, 
                        position = @Position, 
                        basis = @Basis, 
                        rate = @Rate, 
                        password = @Password,
                        shift_schedule = @ShiftSchedule,
                        day_off = @DayOff,
                        cash_advance_balance = @CashAdvanceBalance,
                        date_hired = @DateHired,
                        sss_deduct = @SssDeduct,
                        philhealth_deduct = @PhilhealthDeduct,
                        pagibig_deduct = @PagibigDeduct,
                        tax_deduct = @TaxDeduct
                    WHERE id = @Id;";

                emp.Id = id;

                int rowsAffected = await connection.ExecuteAsync(sql, emp);

                if (rowsAffected > 0)
                    return Ok(new { Message = "Employee updated successfully!" });
                else
                    return NotFound($"Employee with Id {id} not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                return StatusCode(500, $"Error updating employee: {ex.Message}");
            }
        }

        // 4. DELETE EMPLOYEE
        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteEmployee(string name)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                string sql = "DELETE FROM employees WHERE name = @Name";

                int rowsAffected = await connection.ExecuteAsync(sql, new { Name = name });

                if (rowsAffected > 0)
                    return Ok();
                else
                    return NotFound("Employee not found.");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
            {
                return BadRequest("Bawal burahin: May existing payslips o records pa ang empleyadong ito sa system.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}