using System.Data;
using System.Data.SqlClient;

namespace TestAPIServer.Services;

public class TagMappingService : ITagMappingService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TagMappingService> _logger;

    public TagMappingService(IConfiguration configuration, ILogger<TagMappingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [Obsolete]
    public async Task<DataSet?> SelectTagDataAsync(string groupCode)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("데이터베이스 연결 문자열이 설정되지 않았습니다.");
                return null;
            }

            string strProcedureName = "P_TagMappingList";
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(strProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add(new SqlParameter("@IN_WORK_TYPE", "SelectTagData"));
            command.Parameters.Add(new SqlParameter("@IN_GroupCode", groupCode));

            using var adapter = new SqlDataAdapter(command);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);

            return dataSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "태그 데이터 조회 중 오류 발생: GroupCode={GroupCode}", groupCode);
            throw;
        }
    }
}

