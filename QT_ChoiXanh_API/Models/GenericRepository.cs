using GenericWebApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;

namespace GenericWebApi.Repositories
{
    public class GenericRepository : IGenericRepository
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public GenericRepository(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> AddAsync(string tableName, Dictionary<string, object> data)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var columns = string.Join(", ", data.Keys);
            var parameters = string.Join(", ", data.Keys.Select(k => "@" + k));
            var query = $"INSERT INTO [{tableName}] ({columns}) VALUES ({parameters})";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    foreach (var kvp in data)
                    {
                        var value = ConvertValue(kvp.Value);
                        command.Parameters.AddWithValue("@" + kvp.Key, value ?? DBNull.Value);
                    }
                    var affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        public async Task<bool> UpdateAsync(string tableName, Dictionary<string, object> key, Dictionary<string, object> data)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var primaryKey = await GetPrimaryKeyColumnAsync(connection, tableName);

                // Only update columns provided in the data dictionary, excluding the primary key
                var validColumns = data.Keys.Where(k => k != primaryKey).ToList();
                if (validColumns.Count == 0)
                    throw new ArgumentException("No valid columns provided for update.");

                var setClauses = string.Join(", ", validColumns.Select(k => $"[{k}] = @{k}"));
                var whereClause = string.Join(" AND ", key.Select(kvp => $"[{kvp.Key}] = @{kvp.Key}"));
                var query = $"UPDATE [{tableName}] SET {setClauses} WHERE {whereClause}";

                using (var command = new SqlCommand(query, connection))
                {
                    foreach (var kvp in key)
                    {
                        var value = ConvertValue(kvp.Value);
                        command.Parameters.AddWithValue("@" + kvp.Key, value ?? DBNull.Value);
                    }
                    foreach (var column in validColumns)
                    {
                        var value = ConvertValue(data[column]);
                        command.Parameters.AddWithValue("@" + column, value ?? DBNull.Value);
                    }
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> DeleteAsync(string tableName, Dictionary<string, object> key)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var primaryKey = await GetPrimaryKeyColumnAsync(connection, tableName);
                var whereClause = string.Join(" AND ", key.Select(kvp => $"[{kvp.Key}] = @{kvp.Key}"));
                var query = $"DELETE FROM [{tableName}] WHERE {whereClause}";

                using (var command = new SqlCommand(query, connection))
                {
                    foreach (var kvp in key)
                    {
                        var value = ConvertValue(kvp.Value);
                        command.Parameters.AddWithValue("@" + kvp.Key, value ?? DBNull.Value);
                    }
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<Dictionary<string, object>> GetByIdAsync(string tableName, Dictionary<string, object> key, string[]? columns)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var result = new Dictionary<string, object>();
                var columnList = columns != null && columns.Length > 0
                    ? string.Join(", ", columns.Select(c => $"[{c.Trim()}]"))
                    : string.Join(", ", (await GetAllColumnsAsync(connection, tableName)).Select(c => $"[{c}]"));
                var whereClause = string.Join(" AND ", key.Select(kvp => $"[{kvp.Key}] = @{kvp.Key}"));
                var query = $"SELECT {columnList} FROM [{tableName}] WHERE {whereClause}";

                using (var command = new SqlCommand(query, connection))
                {
                    foreach (var kvp in key)
                    {
                        var value = ConvertValue(kvp.Value);
                        command.Parameters.AddWithValue("@" + kvp.Key, value ?? DBNull.Value);
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            return result;
                        }
                    }
                }
            }
            return null;
        }

        public async Task<List<Dictionary<string, object>>> GetAllAsync(string tableName, int page, int pageSize, string[]? columns)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var result = new List<Dictionary<string, object>>();
                var columnList = columns != null && columns.Length > 0
                    ? string.Join(", ", columns.Select(c => $"[{c.Trim()}]"))
                    : string.Join(", ", (await GetAllColumnsAsync(connection, tableName)).Select(c => $"[{c}]"));
                var query = $"SELECT {columnList} FROM [{tableName}] ORDER BY (SELECT NULL) OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var command = new SqlCommand(query, connection) { CommandTimeout = 60 })
                {
                    command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    command.Parameters.AddWithValue("@PageSize", Math.Min(pageSize, 1000));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            result.Add(row);
                        }
                    }
                }
                return result;
            }
        }

        public async Task<string> GetPrimaryKeyColumnAsync(SqlConnection connection, string tableName)
        {
            var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo' AND CONSTRAINT_NAME LIKE 'PK_%'";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName.Replace("dbo.", ""));
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader["COLUMN_NAME"].ToString();
                    }
                }
            }
            throw new InvalidOperationException($"No primary key found for table {tableName}");
        }

        private async Task<List<string>> GetAllColumnsAsync(SqlConnection connection, string tableName)
        {
            var columns = new List<string>();
            var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName.Replace("dbo.", ""));
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add(reader["COLUMN_NAME"].ToString());
                    }
                }
            }
            if (columns.Count == 0)
                throw new InvalidOperationException($"No columns found for table {tableName}");
            return columns;
        }

        private object ConvertValue(object value)
        {
            if (value == null) return null;

            if (value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        if (DateTime.TryParse(jsonElement.GetString(), out DateTime dateTimeValue))
                            return dateTimeValue;
                        return jsonElement.GetString();
                    case JsonValueKind.Number:
                        return jsonElement.GetInt32();
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return jsonElement.GetBoolean();
                    case JsonValueKind.Null:
                        return null;
                    case JsonValueKind.Object:
                    case JsonValueKind.Array:
                        return jsonElement.ToString();
                    default:
                        throw new InvalidOperationException($"Unsupported JsonElement type: {jsonElement.ValueKind}");
                }
            }

            return value;
        }
    }
}