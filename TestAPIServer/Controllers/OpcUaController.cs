using Microsoft.AspNetCore.Mvc;
using TestAPIServer.Models;
using TestAPIServer.Services;

namespace TestAPIServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpcUaController : ControllerBase
{
    private readonly IOpcUaService _opcUaService;
    private readonly ILogger<OpcUaController> _logger;

    public OpcUaController(IOpcUaService opcUaService, ILogger<OpcUaController> logger)
    {
        _opcUaService = opcUaService;
        _logger = logger;
    }

    /// <summary>
    /// OPC UA 서버에 연결
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult<OpcUaResponse<bool>>> Connect([FromBody] OpcUaConnectionRequest request)
    {
        try
        {
            var result = await _opcUaService.ConnectAsync(request);
            return Ok(new OpcUaResponse<bool>
            {
                Success = result,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "연결 요청 처리 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// OPC UA 서버 연결 상태 확인
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<OpcUaResponse<bool>>> GetStatus()
    {
        try
        {
            var isConnected = await _opcUaService.IsConnectedAsync();
            return Ok(new OpcUaResponse<bool>
            {
                Success = true,
                Data = isConnected
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "상태 확인 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// OPC UA 서버 연결 해제
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult<OpcUaResponse<bool>>> Disconnect()
    {
        try
        {
            await _opcUaService.DisconnectAsync();
            return Ok(new OpcUaResponse<bool>
            {
                Success = true,
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "연결 해제 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// OPC UA 노드 값 읽기
    /// </summary>
    [HttpPost("read")]
    public async Task<ActionResult<OpcUaResponse<object>>> Read([FromBody] OpcUaReadRequest request)
    {
        try
        {
            var result = await _opcUaService.ReadValueAsync(request.NodeId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "값 읽기 중 오류 발생");
            return BadRequest(new OpcUaResponse<object>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// OPC UA 여러 노드 값 읽기 (태그 그룹 읽기)
    /// </summary>
    [HttpPost("read-multiple")]
    public async Task<ActionResult<OpcUaResponse<Dictionary<string, object?>>>> ReadMultiple([FromBody] OpcUaReadMultipleRequest request)
    {
        try
        {
            var result = await _opcUaService.ReadMultipleValuesAsync(request.NodeIds);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "여러 값 읽기 중 오류 발생");
            return BadRequest(new OpcUaResponse<Dictionary<string, object?>>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// OPC UA 노드 값 쓰기
    /// </summary>
    [HttpPost("write")]
    public async Task<ActionResult<OpcUaResponse<bool>>> Write([FromBody] OpcUaWriteRequest request)
    {
        try
        {
            var result = await _opcUaService.WriteValueAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "값 쓰기 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// 태그 그룹 모니터링 시작
    /// </summary>
    [HttpPost("monitor/start")]
    public async Task<ActionResult<OpcUaResponse<bool>>> StartMonitoring([FromBody] StartMonitoringRequest request)
    {
        try
        {
            var result = await _opcUaService.StartMonitoringGroupAsync(request.GroupCode);
            return Ok(new OpcUaResponse<bool>
            {
                Success = result,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "모니터링 시작 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// 태그 그룹 모니터링 중지
    /// </summary>
    [HttpPost("monitor/stop")]
    public async Task<ActionResult<OpcUaResponse<bool>>> StopMonitoring([FromBody] TagGroupReadRequest request)
    {
        try
        {
            var result = await _opcUaService.StopMonitoringGroupAsync(request.GroupId);
            return Ok(new OpcUaResponse<bool>
            {
                Success = result,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "모니터링 중지 중 오류 발생");
            return BadRequest(new OpcUaResponse<bool>
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// 태그 값 변경 이벤트 스트리밍 (Server-Sent Events)
    /// </summary>
    [HttpGet("monitor/stream/{groupCode}")]
    public async Task StreamTagChanges(string groupCode, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await foreach (var tagEvent in _opcUaService.SubscribeToTagChangesAsync(groupCode, cancellationToken))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    clientHandle = tagEvent.ClientHandle,
                    value = tagEvent.Value,
                    timestamp = tagEvent.Timestamp
                });

                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 클라이언트 연결 종료
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "스트리밍 중 오류 발생: {GroupCode}", groupCode);
        }
    }
}

