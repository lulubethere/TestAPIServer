using System.Data;

namespace TestAPIServer.Services;

public interface ITagMappingService
{
    Task<DataSet?> SelectTagDataAsync(string groupCode);
}

