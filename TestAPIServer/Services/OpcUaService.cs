using Opc.Ua;
using Opc.Ua.Client;
using TestAPIServer.Models;
using ClientSession = Opc.Ua.Client.Session;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Data;

namespace TestAPIServer.Services;

public class OpcUaService : IOpcUaService, IDisposable
{
    private ClientSession? _session;
    private readonly ILogger<OpcUaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITagMappingService _tagMappingService;
    private ApplicationConfiguration? _applicationConfiguration;
    
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, Channel<TagValueChangedEvent>> _eventChannels = new();

    public OpcUaService(ILogger<OpcUaService> logger, IConfiguration configuration, ITagMappingService tagMappingService)
    {
        _logger = logger;
        _configuration = configuration;
        _tagMappingService = tagMappingService;
    }

    [Obsolete]
    public async Task<bool> ConnectAsync(OpcUaConnectionRequest request)
    {
        var endpointUrl = string.IsNullOrWhiteSpace(request.EndpointUrl)
            ? _configuration["OpcUa:DefaultEndpointUrl"] : request.EndpointUrl;

        var useSecurity = string.IsNullOrWhiteSpace(request.EndpointUrl)
            ? _configuration.GetValue<bool>("OpcUa:UseSecurity", false)
            : request.UseSecurity;
        
        try
        {
            if (_session != null && _session.Connected)
            {
                _logger.LogInformation("이미 연결된 세션이 있습니다. 기존 연결을 재사용합니다.");
                return true;
            }
            
            CleanupSession();

            _logger.LogInformation("OPC UA 서버 연결 시도: {EndpointUrl}", endpointUrl);

            _applicationConfiguration = new ApplicationConfiguration
            {
                ApplicationName = "TestAPIServer",
                ApplicationUri = Utils.Format(@"urn:{0}:TestAPIServer", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = "TestAPIServer" },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            await _applicationConfiguration.ValidateAsync(ApplicationType.Client);

            var userIdentityTokens = new UserTokenPolicyCollection();
            userIdentityTokens.Add(new UserTokenPolicy { TokenType = UserTokenType.Anonymous, PolicyId = "Anonymous" });
            
            if (!string.IsNullOrEmpty(request.Username))
            {
                userIdentityTokens.Add(new UserTokenPolicy { TokenType = UserTokenType.UserName, PolicyId = "Username" });
            }

            var endpointDescription = new EndpointDescription
            {
                EndpointUrl = endpointUrl,
                SecurityMode = useSecurity ? MessageSecurityMode.SignAndEncrypt : MessageSecurityMode.None,
                SecurityPolicyUri = useSecurity ? SecurityPolicies.Basic256Sha256 : SecurityPolicies.None,
                Server = new ApplicationDescription { ApplicationUri = endpointUrl, ApplicationName = "KEPServer" },
                UserIdentityTokens = userIdentityTokens
            };

            var endpointConfig = EndpointConfiguration.Create(_applicationConfiguration);
            endpointConfig.OperationTimeout = 15000;
            var configuredEndpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfig);

            IUserIdentity? userIdentity = null;
            if (!string.IsNullOrEmpty(request.Username))
            {
                var passwordBytes = string.IsNullOrEmpty(request.Password) 
                    ? Array.Empty<byte>() 
                    : System.Text.Encoding.UTF8.GetBytes(request.Password);
                userIdentity = new UserIdentity(request.Username, passwordBytes);
            }

            _session = await ClientSession.Create(
                _applicationConfiguration,
                configuredEndpoint,
                false,
                "TestAPIServer",
                60000,
                userIdentity,
                null);

            if (_session != null && _session.Connected)
            {
                _session.KeepAlive += (session, e) =>
                {
                    if (e.CurrentState != ServerState.Running)
                    {
                        _logger.LogWarning("OPC UA 서버 연결이 끊어졌습니다. 상태: {State}", e.CurrentState);
                        CleanupSubscriptions();
                    }
                };

                _logger.LogInformation("OPC UA 서버에 연결되었습니다: {EndpointUrl}", endpointUrl);
                return true;
            }

            _logger.LogError("세션이 생성되었지만 연결되지 않았습니다: {EndpointUrl}", endpointUrl);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 연결 중 오류 발생: {Message}", ex.Message);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_session == null) return;

        try
        {
            CleanupSubscriptions();
            await _session.CloseAsync(CancellationToken.None);
            _session.Dispose();
            _session = null;
            _logger.LogInformation("OPC UA 연결이 종료되었습니다.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 연결 종료 중 오류 발생: {Message}", ex.Message);
        }
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_session != null && _session.Connected);
    }

    public async Task<OpcUaResponse<object>> ReadValueAsync(string nodeId)
    {
        if (!CheckConnection<object>(out var errorResponse)) return errorResponse!;

        try
        {
            var readValueIdCollection = new ReadValueIdCollection 
            { 
                new ReadValueId { NodeId = new NodeId(nodeId), AttributeId = Attributes.Value }
            };

            var response = await _session!.ReadAsync(null, 0, TimestampsToReturn.Neither, readValueIdCollection, CancellationToken.None);

            if (response.Results == null || response.Results.Count == 0)
            {
                return CreateErrorResponse<object>("읽기 결과가 없습니다.");
            }

            var result = response.Results[0];
            if (StatusCode.IsGood(result.StatusCode))
            {
                return new OpcUaResponse<object> { Success = true, Data = result.Value };
            }

            return CreateErrorResponse<object>($"읽기 실패: {result.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 값 읽기 중 오류 발생: {Message}", ex.Message);
            return CreateErrorResponse<object>(ex.Message);
        }
    }

    public async Task<OpcUaResponse<Dictionary<string, object?>>> ReadMultipleValuesAsync(List<string> nodeIds)
    {
        if (!CheckConnection<Dictionary<string, object?>>(out var errorResponse)) return errorResponse!;

        if (nodeIds == null || nodeIds.Count == 0)
        {
            return CreateErrorResponse<Dictionary<string, object?>>("NodeId 목록이 비어있습니다.");
        }

        try
        {
            var readValueIdCollection = new ReadValueIdCollection();
            foreach (var nodeId in nodeIds)
            {
                readValueIdCollection.Add(new ReadValueId { NodeId = new NodeId(nodeId), AttributeId = Attributes.Value });
            }

            var response = await _session!.ReadAsync(null, 0, TimestampsToReturn.Neither, readValueIdCollection, CancellationToken.None);

            var result = new Dictionary<string, object?>();
            if (response.Results != null && response.Results.Count > 0)
            {
                for (int i = 0; i < response.Results.Count && i < nodeIds.Count; i++)
                {
                    var readResult = response.Results[i];
                    result[nodeIds[i]] = StatusCode.IsGood(readResult.StatusCode) ? readResult.Value : null;
                }
            }

            return new OpcUaResponse<Dictionary<string, object?>> { Success = true, Data = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 여러 값 읽기 중 오류 발생: {Message}", ex.Message);
            return CreateErrorResponse<Dictionary<string, object?>>(ex.Message);
        }
    }

    public async Task<OpcUaResponse<bool>> WriteValueAsync(OpcUaWriteRequest request)
    {
        if (!CheckConnection<bool>(out var errorResponse)) return errorResponse!;

        try
        {
            var dataValue = new DataValue { Value = ConvertValue(request.Value, request.DataType) };
            var writeValue = new WriteValue
            {
                NodeId = new NodeId(request.NodeId),
                AttributeId = Attributes.Value,
                Value = dataValue
            };

            var response = await _session!.WriteAsync(null, new WriteValueCollection { writeValue }, CancellationToken.None);

            if (response.Results == null || response.Results.Count == 0)
            {
                return CreateErrorResponse<bool>("쓰기 결과가 없습니다.");
            }

            var result = response.Results[0];
            if (StatusCode.IsGood(result))
            {
                return new OpcUaResponse<bool> { Success = true, Data = true };
            }

            return CreateErrorResponse<bool>($"쓰기 실패: {result}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC UA 값 쓰기 중 오류 발생: {Message}", ex.Message);
            return CreateErrorResponse<bool>(ex.Message);
        }
    }

    public async Task<bool> StartMonitoringGroupAsync(string groupCode)
    {
        try
        {
            if (_session == null || !_session.Connected)
            {
                _logger.LogWarning("세션이 연결되지 않았습니다. 재연결을 시도합니다. GroupCode: {GroupCode}", groupCode);
                var reconnected = await ConnectAsync(new OpcUaConnectionRequest());
                if (!reconnected)
                {
                    _logger.LogError("세션 재연결에 실패했습니다. GroupCode: {GroupCode}", groupCode);
                    return false;
                }
            }

            CleanupSubscription(groupCode);
            CleanupChannel(groupCode);

            var tagGroup = await _tagMappingService.SelectTagDataAsync(groupCode);
            if (tagGroup == null || tagGroup.Tables.Count == 0 || tagGroup.Tables[0].Rows.Count == 0)
            {
                _logger.LogWarning("태그 데이터가 없습니다. GroupCode: {GroupCode}", groupCode);
                return false;
            }

            var channel = Channel.CreateUnbounded<TagValueChangedEvent>();
            _eventChannels[groupCode] = channel;

            var subscription = new Subscription(_session!.DefaultSubscription)
            {
                DisplayName = $"TagGroup_{groupCode}",
                PublishingEnabled = true,
                PublishingInterval = 100,
                LifetimeCount = 0,
                KeepAliveCount = 0,
                Priority = 0
            };

            _session.AddSubscription(subscription);
#pragma warning disable CS0618
            subscription.Create();
#pragma warning restore CS0618

            var namespaceIndex = GetNamespaceIndex(_session);

            foreach (DataRow row in tagGroup.Tables[0].Rows)
            {
                var addressName = row["AddressName"]?.ToString();
                if (string.IsNullOrEmpty(addressName)) continue;

                if (!int.TryParse(row["ClientHandle"]?.ToString(), out int clientHandle)) continue;

                var updateRate = 1000;
                if (row["UpdateRate"] != null && int.TryParse(row["UpdateRate"].ToString(), out int updateRateValue))
                {
                    updateRate = updateRateValue;
                }

                var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = addressName,
                    StartNodeId = new NodeId(addressName, namespaceIndex),
                    AttributeId = Attributes.Value,
                    MonitoringMode = MonitoringMode.Reporting,
                    SamplingInterval = updateRate
                };

                monitoredItem.Handle = clientHandle;
                monitoredItem.Notification += (item, e) =>
                {
                    if (e.NotificationValue is MonitoredItemNotification notification && StatusCode.IsGood(notification.Value.StatusCode))
                    {
                        channel.Writer.TryWrite(new TagValueChangedEvent
                        {
                            ClientHandle = item.Handle?.ToString() ?? string.Empty,
                            Value = notification.Value.Value,
                            Timestamp = notification.Value.SourceTimestamp
                        });
                    }
                };

                subscription.AddItem(monitoredItem);
            }

            if (subscription.MonitoredItemCount == 0)
            {
                subscription.Dispose();
                _eventChannels.TryRemove(groupCode, out var removedChannel);
                removedChannel?.Writer.Complete();
                return false;
            }

#pragma warning disable CS0618
            subscription.ApplyChanges();
#pragma warning restore CS0618

            _subscriptions[groupCode] = subscription;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "모니터링 시작 중 오류 발생: {GroupCode}", groupCode);
            return false;
        }
    }

    public Task<bool> StopMonitoringGroupAsync(string groupId)
    {
        try
        {
            if (_subscriptions.TryRemove(groupId, out var subscription))
            {
                if (_session != null && _session.Connected)
                {
#pragma warning disable CS0618
                    _session.RemoveSubscription(subscription);
#pragma warning restore CS0618
                }
                subscription.Dispose();
            }

            if (_eventChannels.TryRemove(groupId, out var channel))
            {
                channel.Writer.Complete();
            }

            _logger.LogInformation("태그 그룹 모니터링 중지: {GroupId}", groupId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "태그 그룹 모니터링 중지 중 오류 발생: {GroupId}", groupId);
            return Task.FromResult(false);
        }
    }

    public async IAsyncEnumerable<TagValueChangedEvent> SubscribeToTagChangesAsync(string groupId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_eventChannels.TryGetValue(groupId, out var channel))
        {
            yield break;
        }

        await foreach (var tagEvent in channel.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return tagEvent;
        }
    }

    public void Dispose()
    {
        foreach (var groupId in _subscriptions.Keys.ToList())
        {
            StopMonitoringGroupAsync(groupId).Wait();
        }
        DisconnectAsync().Wait();
    }

    private void CleanupSession()
    {
        if (_session == null) return;

        try
        {
            CleanupSubscriptions();
            _session.CloseAsync(CancellationToken.None).Wait();
            _session.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "기존 세션 정리 중 오류 발생");
        }
        finally
        {
            _session = null;
        }
    }

    private void CleanupSubscriptions()
    {
        foreach (var groupId in _subscriptions.Keys.ToList())
        {
            if (_subscriptions.TryRemove(groupId, out var subscription))
            {
                try
                {
                    if (_session != null && _session.Connected)
                    {
#pragma warning disable CS0618
                        _session.RemoveSubscription(subscription);
#pragma warning restore CS0618
                    }
                    subscription.Dispose();
                }
                catch { }
            }
            if (_eventChannels.TryRemove(groupId, out var channel))
            {
                channel.Writer.Complete();
            }
        }
    }

    private void CleanupSubscription(string groupCode)
    {
        if (!_subscriptions.TryGetValue(groupCode, out var subscription)) return;

        try
        {
            if (_session != null && _session.Connected)
            {
#pragma warning disable CS0618
                _session.RemoveSubscription(subscription);
#pragma warning restore CS0618
            }
            subscription.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "기존 Subscription 정리 중 오류 발생: {GroupCode}", groupCode);
        }
        finally
        {
            _subscriptions.TryRemove(groupCode, out _);
        }
    }

    private void CleanupChannel(string groupCode)
    {
        if (!_eventChannels.TryGetValue(groupCode, out var channel)) return;

        if (!channel.Reader.Completion.IsCompleted)
        {
            channel.Writer.Complete();
        }
        _eventChannels.TryRemove(groupCode, out _);
    }

    private bool CheckConnection<T>(out OpcUaResponse<T>? errorResponse)
    {
        if (_session == null || !_session.Connected)
        {
            errorResponse = CreateErrorResponse<T>("OPC UA 서버에 연결되어 있지 않습니다.");
            return false;
        }
        errorResponse = null;
        return true;
    }

    private OpcUaResponse<T> CreateErrorResponse<T>(string errorMessage)
    {
        return new OpcUaResponse<T> { Success = false, ErrorMessage = errorMessage };
    }

    private ushort GetNamespaceIndex(ClientSession session)
    {
        if (session.NamespaceUris == null) return 0;
        return session.NamespaceUris.Count > 2 ? (ushort)2 : (session.NamespaceUris.Count > 1 ? (ushort)1 : (ushort)0);
    }

    private object? ConvertValue(object value, string? dataType)
    {
        if (dataType == null) return value;

        return dataType.ToLower() switch
        {
            "int" or "int32" => Convert.ToInt32(value),
            "double" or "float" => Convert.ToDouble(value),
            "bool" or "boolean" => Convert.ToBoolean(value),
            "string" => value.ToString(),
            _ => value
        };
    }
}
