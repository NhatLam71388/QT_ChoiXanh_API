using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace GenericWebApi.Repositories
{
    public interface IGenericRepository
    {
        Task<bool> AddAsync(string tableName, Dictionary<string, object> data);
        Task<bool> UpdateAsync(string tableName, Dictionary<string, object> key, Dictionary<string, object> data);
        Task<bool> DeleteAsync(string tableName, Dictionary<string, object> key);
        Task<Dictionary<string, object>> GetByIdAsync(string tableName, Dictionary<string, object> key);
        Task<List<Dictionary<string, object>>> GetAllAsync(string tableName, int page = 1, int pageSize = 100);
        Task<string> GetPrimaryKeyColumnAsync(SqlConnection sqlConnection, string tableName);
    }
}