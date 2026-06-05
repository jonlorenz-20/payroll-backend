using Microsoft.AspNetCore.Mvc;
using Npgsql;
using payroll.API.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DepartmentsController : ControllerBase
    {
        private readonly string _connectionString;

        public DepartmentsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var list = new List<DepartmentModel>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT id, name FROM departments ORDER BY name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DepartmentModel { Id = reader.GetInt32(0), Name = reader.GetString(1) });
            }
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddDepartment([FromBody] DepartmentModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name)) return BadRequest("Name cannot be empty.");
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("INSERT INTO departments (name) VALUES (@name) RETURNING id", conn);
            cmd.Parameters.AddWithValue("name", model.Name.Trim());
            try
            {
                var id = await cmd.ExecuteScalarAsync();
                model.Id = Convert.ToInt32(id);
                return Ok(model);
            }
            catch { return BadRequest("Department already exists."); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM departments WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(true);
        }

        [HttpGet("{deptId}/positions")]
        public async Task<IActionResult> GetPositions(int deptId)
        {
            var list = new List<PositionModel>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT id, department_id, name FROM positions WHERE department_id = @deptId ORDER BY name", conn);
            cmd.Parameters.AddWithValue("deptId", deptId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PositionModel { Id = reader.GetInt32(0), DepartmentId = reader.GetInt32(1), Name = reader.GetString(2) });
            }
            return Ok(list);
        }

        [HttpPost("positions")]
        public async Task<IActionResult> AddPosition([FromBody] PositionModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name)) return BadRequest("Name cannot be empty.");
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("INSERT INTO positions (department_id, name) VALUES (@deptId, @name) RETURNING id", conn);
            cmd.Parameters.AddWithValue("deptId", model.DepartmentId);
            cmd.Parameters.AddWithValue("name", model.Name.Trim());
            try
            {
                var id = await cmd.ExecuteScalarAsync();
                model.Id = Convert.ToInt32(id);
                return Ok(model);
            }
            catch { return BadRequest("Position already exists."); }
        }

        [HttpDelete("positions/{id}")]
        public async Task<IActionResult> DeletePosition(int id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM positions WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Ok(true);
        }
    }
}