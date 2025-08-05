using GenericWebApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace GenericWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GenericController : ControllerBase
    {
        private readonly IGenericRepository _repository;
        private readonly IConfiguration _configuration;

        public GenericController(IGenericRepository repository, IConfiguration configuration)
        {
            _repository = repository;
            _configuration = configuration;
        }

        [HttpPost("{tableName}")]
        public async Task<IActionResult> Add(string tableName, [FromBody] Dictionary<string, object> data)
        {
            try
            {
                var result = await _repository.AddAsync(tableName, data);
                if (result == null || result.Count == 0)
                    return BadRequest(new { error = "Failed to add entity and retrieve new ID." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error adding entity: {ex.Message}" });
            }
        }

        [HttpPut("{tableName}/{keyValue}")]
        public async Task<IActionResult> Update(string tableName, string keyValue, [FromBody] Dictionary<string, object> data)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var primaryKey = await _repository.GetPrimaryKeyColumnAsync(connection, tableName);
                    var key = new Dictionary<string, object> { { primaryKey, ConvertKeyValue(keyValue) } };
                    var success = await _repository.UpdateAsync(tableName, key, data);
                    if (!success)
                        return NotFound(new { error = $"Entity with key {primaryKey}={keyValue} not found in table {tableName}" });
                    return Ok(new { message = "Entity updated successfully" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error updating entity: {ex.Message}" });
            }
        }

        [HttpDelete("{tableName}/{keyValue}")]
        public async Task<IActionResult> Delete(string tableName, string keyValue)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var primaryKey = await _repository.GetPrimaryKeyColumnAsync(connection, tableName);
                    var key = new Dictionary<string, object> { { primaryKey, ConvertKeyValue(keyValue) } };
                    var success = await _repository.DeleteAsync(tableName, key);
                    if (!success)
                        return NotFound(new { error = $"Entity with key {primaryKey}={keyValue} not found in table {tableName}" });
                    return Ok(new { message = "Entity deleted successfully" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error deleting entity: {ex.Message}" });
            }
        }

        [HttpGet("{tableName}/{keyValue}")]
        public async Task<IActionResult> GetById(string tableName, string keyValue)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var primaryKey = await _repository.GetPrimaryKeyColumnAsync(connection, tableName);
                    var key = new Dictionary<string, object> { { primaryKey, ConvertKeyValue(keyValue) } };
                    var entity = await _repository.GetByIdAsync(tableName, key);
                    if (entity == null)
                        return NotFound(new { error = $"Entity with key {primaryKey}={keyValue} not found in table {tableName}" });
                    return Ok(entity);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error retrieving entity: {ex.Message}" });
            }
        }

        [HttpGet("{tableName}")]
        public async Task<IActionResult> GetAll(string tableName)
        {
            try
            {
                var entities = await _repository.GetAllAsync(tableName);
                return Ok(entities);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Error retrieving entities: {ex.Message}" });
            }
        }

        private object ConvertKeyValue(string keyValue)
        {
            if (int.TryParse(keyValue, out int intValue))
                return intValue;
            if (Guid.TryParse(keyValue, out Guid guidValue))
                return guidValue;
            return keyValue;
        }
    }
}