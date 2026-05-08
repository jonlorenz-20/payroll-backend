using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly string _connectionString;

        public EmployeesController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                // Siguraduhin na ang AS names ay tugma sa C# Property names
                string sql = @"SELECT 
                                id AS Id, 
                                biometric_id AS BiometricId, 
                                name AS Name, 
                                email AS Email, 
                                department AS Department, 
                                basis AS Basis, 
                                rate AS Rate, 
                                username AS Username, 
                                password AS Password, 
                                shift_schedule AS ShiftSchedule, 
                                cash_advance_balance AS CashAdvanceBalance, 
                                date_hired AS DateHired 
                               FROM employees";

                var employees = await connection.QueryAsync<EmployeeModel>(sql);
                return Ok(employees.OrderBy(e => e.Name));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database Error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveEmployee([FromBody] EmployeeModel emp)
        {
            if (emp == null) return BadRequest("Employee data is null.");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                // TANDAAN: Wag isama ang 'id' sa INSERT columns kung SERIAL ito sa database.
                // Ang Dapper ay gagamit ng @PropertyName para i-map ang values mula sa EmployeeModel object.
                string sql = @"
                    INSERT INTO employees 
                    (biometric_id, name, email, department, basis, rate, username, password, shift_schedule, cash_advance_balance, date_hired) 
                    VALUES 
                    (@BiometricId, @Name, @Email, @Department, @Basis, @Rate, @Username, @Password, @ShiftSchedule, @CashAdvanceBalance, @DateHired)
                    ON CONFLICT (username) DO UPDATE 
                    SET name = EXCLUDED.name, 
                        email = EXCLUDED.email, 
                        department = EXCLUDED.department, 
                        basis = EXCLUDED.basis, 
                        rate = EXCLUDED.rate, 
                        password = EXCLUDED.password,
                        biometric_id = EXCLUDED.biometric_id,
                        shift_schedule = EXCLUDED.shift_schedule,
                        cash_advance_balance = EXCLUDED.cash_advance_balance,
                        date_hired = EXCLUDED.date_hired;";

                await connection.ExecuteAsync(sql, emp);
                return Ok(new { Message = "Employee saved successfully!" });
            }
            catch (Exception ex)
            {
                // Kung may error, lalabas dito kung anong column ang may problema (hal. null constraint)
                Console.WriteLine($"Save Error: {ex.Message}");
                return StatusCode(500, $"Error saving employee: {ex.Message}");
            }
        }

        // ==========================================================
        // BAGONG ADDED: DELETE ENDPOINT
        // ==========================================================
        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteEmployee(string name)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);

                string sql = "DELETE FROM employees WHERE name = @Name";

                // Gagamit tayo ng Dapper's ExecuteAsync para mabilis at malinis
                int rowsAffected = await connection.ExecuteAsync(sql, new { Name = name });

                if (rowsAffected > 0)
                    return Ok();
                else
                    return NotFound("Employee not found.");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
            {
                // SQLState 23503 = Foreign Key Violation (Ibig sabihin, may Payslip pa siya sa database)
                return BadRequest("Bawal burahin: May existing payslips o records pa ang empleyadong ito sa system.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}