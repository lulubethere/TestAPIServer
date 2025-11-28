namespace TestAPIServer.Models;

public class OpcUaReadMultipleRequest
{
    public List<string> NodeIds { get; set; } = new();
}

