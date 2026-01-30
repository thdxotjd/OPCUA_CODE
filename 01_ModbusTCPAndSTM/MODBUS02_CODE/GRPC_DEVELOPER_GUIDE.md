# DeviceConnector - gRPC 개발자 가이드

## 개요

이 라이브러리는 KEPServerEX OPC UA 서버를 통해 산업용 디바이스와 통신합니다.
gRPC 서비스에서 이 라이브러리를 사용하여 디바이스 데이터를 읽고 쓸 수 있습니다.

---

## 지원 디바이스

| 디바이스 | 서비스 클래스 | 데이터 모델 |
|----------|---------------|-------------|
| ESP32 ModbusTCP | `OpcUaClientService` | `ESP32Data` |
| STM_yolo | `STMYoloClientService` | `STMYoloData` |

---

## 설치

```bash
# 프로젝트 참조 추가
dotnet add reference ../DeviceConnector/DeviceConnector.csproj
```

또는 NuGet 패키지로 빌드 후 참조:
```bash
dotnet pack -c Release
dotnet add package DeviceConnector --source ./nupkg
```

---

## STM_yolo 디바이스

### 태그 구조 (DeviceName: STM, TagGroup: Stm_yolo)

**NodeId 형식:** `ns=2;s=STM.Stm_yolo.TagName`

```
┌──────────────────────────────────────────────────────────────────────┐
│ Write Tags (Target) - gRPC → OPC UA → PLC                            │
├───────────────────┬────────────────────────────────────┬─────────────┤
│ Tag Name          │ NodeId                             │ Type        │
├───────────────────┼────────────────────────────────────┼─────────────┤
│ TargetState       │ ns=2;s=STM.Stm_yolo.TargetState    │ Int64       │
│ TargetSpeedMain   │ ns=2;s=STM.Stm_yolo.TargetSpeedMain│ Int64       │
│ TargetSpeedSort   │ ns=2;s=STM.Stm_yolo.TargetSpeedSort│ Int64       │
│ TargetSpeedLoad   │ ns=2;s=STM.Stm_yolo.TargetSpeedLoad│ Int64       │
│ AgvSortArrived    │ ns=2;s=STM.Stm_yolo.AgvSortArrived │ Boolean     │
│ AgvSortDeparted   │ ns=2;s=STM.Stm_yolo.AgvSortDeparted│ Boolean     │
│ AgvLoadArrived    │ ns=2;s=STM.Stm_yolo.AgvLoadArrived │ Boolean     │
│ AgvLoadDeparted   │ ns=2;s=STM.Stm_yolo.AgvLoadDeparted│ Boolean     │
├───────────────────┴────────────────────────────────────┴─────────────┤
│ Read Tags (Current) - PLC → OPC UA → gRPC                            │
├───────────────────┬────────────────────────────────────┬─────────────┤
│ CurrentState      │ ns=2;s=STM.Stm_yolo.CurrentState   │ Int64       │
│ CurrentSpeedMain  │ ns=2;s=STM.Stm_yolo.CurrentSpeedMain│ Int64      │
│ CurrentSpeedSort  │ ns=2;s=STM.Stm_yolo.CurrentSpeedSort│ Int64      │
│ CurrentSpeedLoad  │ ns=2;s=STM.Stm_yolo.CurrentSpeedLoad│ Int64      │
│ CurrentFloor      │ ns=2;s=STM.Stm_yolo.CurrentFloor   │ Int64       │
│ IsLiftMoving      │ ns=2;s=STM.Stm_yolo.IsLiftMoving   │ Boolean     │
│ IsRobotWorking    │ ns=2;s=STM.Stm_yolo.IsRobotWorking │ Boolean     │
│ IsRobotDone       │ ns=2;s=STM.Stm_yolo.IsRobotDone    │ Boolean     │
└───────────────────┴────────────────────────────────────┴─────────────┘
```

### 데이터 모델

```csharp
public class STMYoloData
{
    // 식별 정보
    public string DeviceId { get; set; }      // "STM_yolo"
    public string ChannelName { get; set; }   // "Channel1_opcua"
    public string DeviceName { get; set; }    // "STM"

    // Write Tags (Target)
    public long TargetState { get; set; }
    public long TargetSpeedMain { get; set; }
    public long TargetSpeedSort { get; set; }
    public long TargetSpeedLoad { get; set; }
    public bool AgvSortArrived { get; set; }
    public bool AgvSortDeparted { get; set; }
    public bool AgvLoadArrived { get; set; }
    public bool AgvLoadDeparted { get; set; }

    // Read Tags (Current)
    public long CurrentState { get; set; }
    public long CurrentSpeedMain { get; set; }
    public long CurrentSpeedSort { get; set; }
    public long CurrentSpeedLoad { get; set; }
    public long CurrentFloor { get; set; }
    public bool IsLiftMoving { get; set; }
    public bool IsRobotWorking { get; set; }
    public bool IsRobotDone { get; set; }

    // 메타 데이터
    public DateTime Timestamp { get; set; }
    public bool IsGoodQuality { get; set; }
}
```

---

## gRPC 서비스에서 사용하기

### 1. DI 등록 (Program.cs 또는 Startup.cs)

```csharp
using DeviceConnector.Models;
using DeviceConnector.Services;

// 연결 설정
var connectionInfo = new OpcUaConnectionInfo
{
    EndpointUrl = "opc.tcp://127.0.0.1:49320",
    AutoReconnect = true,
    ReconnectInterval = 5000,
    PublishingInterval = 100,
    SamplingInterval = 100
};

// STM_yolo 태그 설정
var stmTagConfig = new STMYoloTagConfig
{
    DeviceId = "STM_yolo",
    ChannelName = "Channel1_opcua",
    DeviceName = "STM",
    TagGroupName = "Stm_yolo",
    NamespaceIndex = 2
};

// Singleton으로 등록
builder.Services.AddSingleton(connectionInfo);
builder.Services.AddSingleton(stmTagConfig);
builder.Services.AddSingleton<ISTMYoloClientService, STMYoloClientService>();
```

### 2. gRPC 서비스 구현 예시

```csharp
using DeviceConnector.Interfaces;
using DeviceConnector.Models;
using Grpc.Core;

public class ConveyorService : Conveyor.ConveyorBase
{
    private readonly ISTMYoloClientService _stmClient;
    private readonly ILogger<ConveyorService> _logger;

    public ConveyorService(ISTMYoloClientService stmClient, ILogger<ConveyorService> logger)
    {
        _stmClient = stmClient;
        _logger = logger;
    }

    // 연결 (앱 시작 시 호출)
    public override async Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
    {
        var result = await _stmClient.ConnectAsync(context.CancellationToken);
        return new ConnectResponse { Success = result };
    }

    // 전체 데이터 읽기
    public override async Task<STMYoloDataResponse> GetCurrentData(Empty request, ServerCallContext context)
    {
        var data = await _stmClient.ReadAllDataAsync(context.CancellationToken);
        
        if (data == null)
            throw new RpcException(new Status(StatusCode.Unavailable, "Failed to read data"));

        return new STMYoloDataResponse
        {
            // Target
            TargetState = data.TargetState,
            TargetSpeedMain = data.TargetSpeedMain,
            TargetSpeedSort = data.TargetSpeedSort,
            TargetSpeedLoad = data.TargetSpeedLoad,
            AgvSortArrived = data.AgvSortArrived,
            AgvSortDeparted = data.AgvSortDeparted,
            AgvLoadArrived = data.AgvLoadArrived,
            AgvLoadDeparted = data.AgvLoadDeparted,
            // Current
            CurrentState = data.CurrentState,
            CurrentSpeedMain = data.CurrentSpeedMain,
            CurrentSpeedSort = data.CurrentSpeedSort,
            CurrentSpeedLoad = data.CurrentSpeedLoad,
            CurrentFloor = data.CurrentFloor,
            IsLiftMoving = data.IsLiftMoving,
            IsRobotWorking = data.IsRobotWorking,
            IsRobotDone = data.IsRobotDone,
            // Meta
            Timestamp = Timestamp.FromDateTime(data.Timestamp),
            IsGoodQuality = data.IsGoodQuality
        };
    }

    // 상태 설정
    public override async Task<WriteResponse> SetTargetState(SetTargetStateRequest request, ServerCallContext context)
    {
        var result = await _stmClient.WriteTargetStateAsync(request.State, context.CancellationToken);
        return new WriteResponse { Success = result };
    }

    // 속도 설정 (3개 한번에)
    public override async Task<WriteResponse> SetAllSpeeds(SetSpeedsRequest request, ServerCallContext context)
    {
        var result = await _stmClient.WriteAllSpeedsAsync(
            request.SpeedMain, 
            request.SpeedSort, 
            request.SpeedLoad, 
            context.CancellationToken);
        return new WriteResponse { Success = result };
    }

    // AGV 플래그 설정
    public override async Task<WriteResponse> SetAgvFlag(SetAgvFlagRequest request, ServerCallContext context)
    {
        bool result = request.FlagType switch
        {
            AgvFlagType.SortArrived => await _stmClient.WriteAgvSortArrivedAsync(request.Value),
            AgvFlagType.SortDeparted => await _stmClient.WriteAgvSortDepartedAsync(request.Value),
            AgvFlagType.LoadArrived => await _stmClient.WriteAgvLoadArrivedAsync(request.Value),
            AgvFlagType.LoadDeparted => await _stmClient.WriteAgvLoadDepartedAsync(request.Value),
            _ => false
        };
        return new WriteResponse { Success = result };
    }

    // 실시간 스트림 (Server Streaming)
    public override async Task StreamData(Empty request, 
        IServerStreamWriter<STMYoloDataResponse> responseStream, 
        ServerCallContext context)
    {
        // 데이터 변경 이벤트 구독
        var tcs = new TaskCompletionSource<bool>();
        
        void OnDataChanged(object? sender, STMYoloDataChangedEventArgs e)
        {
            var response = MapToResponse(e.Data);
            responseStream.WriteAsync(response).Wait();
        }

        _stmClient.DataChanged += OnDataChanged;
        await _stmClient.StartAllSubscriptionsAsync(context.CancellationToken);

        try
        {
            // 클라이언트가 연결을 끊을 때까지 대기
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected from stream");
        }
        finally
        {
            _stmClient.DataChanged -= OnDataChanged;
            await _stmClient.StopSubscriptionAsync();
        }
    }

    private static STMYoloDataResponse MapToResponse(STMYoloData data)
    {
        return new STMYoloDataResponse
        {
            CurrentState = data.CurrentState,
            CurrentSpeedMain = data.CurrentSpeedMain,
            CurrentSpeedSort = data.CurrentSpeedSort,
            CurrentSpeedLoad = data.CurrentSpeedLoad,
            CurrentFloor = data.CurrentFloor,
            IsLiftMoving = data.IsLiftMoving,
            IsRobotWorking = data.IsRobotWorking,
            IsRobotDone = data.IsRobotDone,
            Timestamp = Timestamp.FromDateTime(data.Timestamp),
            IsGoodQuality = data.IsGoodQuality
        };
    }
}
```

### 3. Proto 파일 예시

```protobuf
syntax = "proto3";

option csharp_namespace = "YourProject.Grpc";

package conveyor;

service Conveyor {
    rpc Connect(ConnectRequest) returns (ConnectResponse);
    rpc GetCurrentData(Empty) returns (STMYoloDataResponse);
    rpc SetTargetState(SetTargetStateRequest) returns (WriteResponse);
    rpc SetAllSpeeds(SetSpeedsRequest) returns (WriteResponse);
    rpc SetAgvFlag(SetAgvFlagRequest) returns (WriteResponse);
    rpc StreamData(Empty) returns (stream STMYoloDataResponse);
}

message Empty {}

message ConnectRequest {}
message ConnectResponse { bool success = 1; }
message WriteResponse { bool success = 1; }

message SetTargetStateRequest { int64 state = 1; }
message SetSpeedsRequest {
    int64 speed_main = 1;
    int64 speed_sort = 2;
    int64 speed_load = 3;
}

message SetAgvFlagRequest {
    AgvFlagType flag_type = 1;
    bool value = 2;
}

enum AgvFlagType {
    SORT_ARRIVED = 0;
    SORT_DEPARTED = 1;
    LOAD_ARRIVED = 2;
    LOAD_DEPARTED = 3;
}

message STMYoloDataResponse {
    // Target
    int64 target_state = 1;
    int64 target_speed_main = 2;
    int64 target_speed_sort = 3;
    int64 target_speed_load = 4;
    bool agv_sort_arrived = 5;
    bool agv_sort_departed = 6;
    bool agv_load_arrived = 7;
    bool agv_load_departed = 8;
    // Current
    int64 current_state = 9;
    int64 current_speed_main = 10;
    int64 current_speed_sort = 11;
    int64 current_speed_load = 12;
    int64 current_floor = 13;
    bool is_lift_moving = 14;
    bool is_robot_working = 15;
    bool is_robot_done = 16;
    // Meta
    google.protobuf.Timestamp timestamp = 17;
    bool is_good_quality = 18;
}
```

---

## 이벤트 처리

### 연결 상태 변경

```csharp
_stmClient.ConnectionChanged += (sender, e) =>
{
    _logger.LogInformation("Connection: {Previous} → {Current}", 
        e.PreviousState, e.Status.State);
    
    if (e.Status.State == ConnectionState.Disconnected)
    {
        // 재연결 로직 또는 알림
    }
};
```

### 데이터 변경 (구독 시)

```csharp
_stmClient.DataChanged += (sender, e) =>
{
    _logger.LogDebug("Tag changed: {Tag} = {Value}", 
        e.ChangedTagName, e.NewValue);
};
```

### Write 완료

```csharp
_stmClient.WriteCompleted += (sender, e) =>
{
    if (!e.Success)
    {
        _logger.LogError("Write failed: {Tag} - {Error}", 
            e.TagName, e.ErrorMessage);
    }
};
```

### 에러 발생

```csharp
_stmClient.ErrorOccurred += (sender, e) =>
{
    _logger.LogError(e.Exception, "OPC UA Error: {Message}", e.Message);
};
```

---

## API 레퍼런스

### ISTMYoloClientService 인터페이스

| 메서드 | 반환 | 설명 |
|--------|------|------|
| `ConnectAsync()` | `Task<bool>` | OPC UA 서버 연결 |
| `DisconnectAsync()` | `Task` | 연결 해제 |
| `ReadAllDataAsync()` | `Task<STMYoloData?>` | 모든 태그 읽기 |
| `ReadCurrentDataAsync()` | `Task<STMYoloData?>` | Current 태그만 읽기 |
| `ReadTargetDataAsync()` | `Task<STMYoloData?>` | Target 태그만 읽기 |
| `WriteTargetStateAsync(long)` | `Task<bool>` | TargetState 쓰기 |
| `WriteTargetSpeedMainAsync(long)` | `Task<bool>` | 메인 속도 쓰기 |
| `WriteTargetSpeedSortAsync(long)` | `Task<bool>` | 분류 속도 쓰기 |
| `WriteTargetSpeedLoadAsync(long)` | `Task<bool>` | 적재 속도 쓰기 |
| `WriteAllSpeedsAsync(main, sort, load)` | `Task<bool>` | 3개 속도 한번에 쓰기 |
| `WriteAgvSortArrivedAsync(bool)` | `Task<bool>` | AGV 분류 도착 플래그 |
| `WriteAgvSortDepartedAsync(bool)` | `Task<bool>` | AGV 분류 출발 플래그 |
| `WriteAgvLoadArrivedAsync(bool)` | `Task<bool>` | AGV 적재 도착 플래그 |
| `WriteAgvLoadDepartedAsync(bool)` | `Task<bool>` | AGV 적재 출발 플래그 |
| `WriteTagAsync(tagName, value)` | `Task<bool>` | 범용 태그 쓰기 |
| `StartAllSubscriptionsAsync()` | `Task` | 모든 태그 구독 |
| `StartCurrentSubscriptionAsync()` | `Task` | Current 태그만 구독 |
| `StopSubscriptionAsync()` | `Task` | 구독 중지 |

### 속성

| 속성 | 타입 | 설명 |
|------|------|------|
| `IsConnected` | `bool` | 연결 여부 |
| `Status` | `ConnectionStatus` | 상세 연결 상태 |
| `CurrentData` | `STMYoloData?` | 캐시된 현재 데이터 |

---

## 주의사항

1. **Singleton 사용**: OPC UA 세션은 비용이 크므로 Singleton으로 등록
2. **연결 관리**: 앱 시작 시 `ConnectAsync()`, 종료 시 `Dispose()` 호출
3. **스레드 안전**: 내부적으로 `SemaphoreSlim`으로 동기화됨
4. **재연결**: `AutoReconnect = true` 설정 시 자동 재연결

---

## 문의

KEPServerEX 태그 설정 또는 OPC UA 연결 문제는 담당자에게 문의하세요.
