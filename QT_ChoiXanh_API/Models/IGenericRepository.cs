using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace GenericWebApi.Repositories
{
    public interface IGenericRepository
    {
        Task<Dictionary<string, object>> AddAsync(string tableName, Dictionary<string, object> data);
        Task<bool> UpdateAsync(string tableName, Dictionary<string, object> key, Dictionary<string, object> data);
        Task<bool> DeleteAsync(string tableName, Dictionary<string, object> key);
        Task<Dictionary<string, object>> GetByIdAsync(string tableName, Dictionary<string, object> key);
        Task<List<Dictionary<string, object>>> GetAllAsync(string tableName);
        Task<string> GetPrimaryKeyColumnAsync(SqlConnection sqlConnection, string tableName);
    }
}