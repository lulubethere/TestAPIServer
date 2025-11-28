namespace TestAPIServer.Models;

public class OpcUaConnectionRequest
{
    public string EndpointUrl { get; set; } = string.Empty;
    public bool UseSecurity { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

