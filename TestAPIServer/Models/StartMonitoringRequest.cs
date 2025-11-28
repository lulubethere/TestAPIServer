namespace TestAPIServer.Models;

public class StartMonitoringRequest
{
    public string GroupCode { get; set; } = string.Empty; // 그룹 코드만 받음 (예: "G0001")
}

