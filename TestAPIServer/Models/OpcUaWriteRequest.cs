namespace TestAPIServer.Models;

public class OpcUaWriteRequest
{
    public string NodeId { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
    public string? DataType { get; set; }
}

