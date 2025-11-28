using TestAPIServer.Models;

namespace TestAPIServer.Services;

public interface IOpcUaService
{
    Task<bool> ConnectAsync(OpcUaConnectionRequest request);
    Task DisconnectAsync();
    Task<bool> IsConnectedAsync();
    Task<OpcUaResponse<object>> ReadValueAsync(string nodeId);
    Task<OpcUaResponse<Dictionary<string, object?>>> ReadMultipleValuesAsync(List<string> nodeIds);
    Task<OpcUaResponse<bool>> WriteValueAsync(OpcUaWriteRequest request);
    Task<bool> StartMonitoringGroupAsync(string groupCode);
    Task<bool> StopMonitoringGroupAsync(string groupId);
    IAsyncEnumerable<TagValueChangedEvent> SubscribeToTagChangesAsync(string groupId, CancellationToken cancellationToken);
}

public class TagValueChangedEvent
{
    public string ClientHandle { get; set; } = string.Empty; // ClientHandle (int를 문자열로)
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

