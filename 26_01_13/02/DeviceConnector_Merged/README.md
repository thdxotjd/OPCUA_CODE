# DeviceConnector

ESP32 ModbusTCP / RoboDK 로봇 - OPC UA - gRPC 통신 클래스 라이브러리  
**ROS_ControlHub 통합 지원**

---

## 📋 개요

DeviceConnector는 산업용 장비 통신을 위한 .NET 8 클래스 라이브러리입니다.

### 주요 기능
1. **OPC UA 통신** - KEPServerEX를 통한 ESP32/PLC 데이터 읽기/쓰기
2. **RoboDK 로봇 제어** ⭐ NEW - OPC UA Method 호출을 통한 로봇 Joint 제어
3. **ROS_ControlHub 통합** - gRPC/SignalR을 통한 양방향 통신
4. **실시간 구독** - OPC UA Subscription을 통한 데이터 변경 감지
5. **자동 재연결** - 연결 끊김 시 자동 복구

---

## 🏗️ 아키텍처

```
┌──────────────────────────────────────────────────────────────┐
│                        DeviceConnector                        │
├───────────────────────────┬──────────────────────────────────┤
│      OPC UA 클라이언트     │      ROS_ControlHub 클라이언트    │
│   (OpcUaClientService)    │     (RosControlHubClient)        │
│   (RoboDkOpcUaService)    │                                  │
├───────────────────────────┴──────────────────────────────────┤
│                   IntegratedDeviceHub                        │
│            (통합 관리 + 상태 동기화)                           │
└───────────────────────────┬──────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  KEPServerEX  │   │ ROS_ControlHub│   │    RoboDK     │
│  (OPC UA)     │   │   (gRPC)      │   │  (OPC UA)     │
└───────┬───────┘   └───────────────┘   └───────┬───────┘
        │                                       │
        ▼                                       ▼
┌───────────────┐                       ┌───────────────┐
│ ESP32/PLC     │                       │ ABB Robot     │
│ (Modbus TCP)  │                       │ (Simulation)  │
└───────────────┘                       └───────────────┘
```

---

## 🤖 RoboDK 로봇 제어 (NEW)

### 왜 RoboDkOpcUaService가 필요한가?

KEPServerEX를 통해 RoboDK에 연결하면 `getJoints`, `setJoints` 같은 **Method 노드**를 읽을 수 없습니다.  
KEPServerEX는 **Variable 노드**만 지원하기 때문에 "The attribute is not supported" 오류가 발생합니다.

**RoboDkOpcUaService**는 OPC UA **Method Call** 기능으로 RoboDK에 직접 연결하여 이 문제를 해결합니다.

### 기본 사용법

```csharp
using DeviceConnector.Services;
using DeviceConnector.Models;

// 1. 서비스 생성 및 연결
var connectionInfo = new RoboDkConnectionInfo
{
    EndpointUrl = "opc.tcp://localhost:4840",  // RoboDK OPC UA 서버
    DefaultRobotName = "ABB CRB 1300-7/1.4"
};

using var roboDk = new RoboDkOpcUaService(connectionInfo);
await roboDk.ConnectAsync();

// 2. Joint 값 읽기 (Method 호출)
string? joints = await roboDk.GetJointsStrAsync("ABB CRB 1300-7/1.4");
Console.WriteLine($"Current Joints: {joints}");  // 예: "0,0,0,0,0,0"

// 3. Joint 값 설정
await roboDk.SetJointsStrAsync("ABB CRB 1300-7/1.4", "10,20,30,0,0,0");

// 4. 시뮬레이션 정보 읽기
var speed = await roboDk.GetSimulationSpeedAsync();
var station = await roboDk.GetStationNameAsync();
Console.WriteLine($"Speed: {speed}, Station: {station}");
```

### ASP.NET Core DI 등록

**appsettings.json:**
```json
{
  "RoboDk": {
    "EndpointUrl": "opc.tcp://localhost:4840",
    "DefaultRobotName": "ABB CRB 1300-7/1.4",
    "AutoReconnect": true
  }
}
```

**Program.cs:**
```csharp
// RoboDK만 사용
builder.Services.AddRoboDkOpcUaService(builder.Configuration);

// ESP32 + RoboDK 함께 사용
builder.Services.AddDeviceConnectorWithRoboDk(builder.Configuration);
```

**Controller:**
```csharp
[ApiController]
[Route("api/robot")]
public class RobotController : ControllerBase
{
    private readonly IRoboDkOpcUaService _roboDk;

    public RobotController(IRoboDkOpcUaService roboDk) => _roboDk = roboDk;

    [HttpGet("joints/{robotName}")]
    public async Task<IActionResult> GetJoints(string robotName)
    {
        if (!_roboDk.IsConnected) await _roboDk.ConnectAsync();
        var joints = await _roboDk.GetJointsStrAsync(robotName);
        return Ok(new { robotName, joints });
    }

    [HttpPost("joints/{robotName}")]
    public async Task<IActionResult> SetJoints(string robotName, [FromBody] string jointsStr)
    {
        var success = await _roboDk.SetJointsStrAsync(robotName, jointsStr);
        return success ? Ok() : BadRequest();
    }
}
```

---

## 📦 설치 및 설정

### 1. 프로젝트 참조
```xml
<ProjectReference Include="..\DeviceConnector\DeviceConnector.csproj" />
```

### 2. appsettings.json 설정
```json
{
  "OpcUa": {
    "EndpointUrl": "opc.tcp://localhost:49320",
    "SessionName": "DeviceConnectorSession",
    "AutoReconnect": true,
    "ReconnectIntervalSeconds": 5
  },
  "DeviceTag": {
    "DeviceId": "ESP32_01",
    "ChannelName": "ModbusTCP",
    "DeviceName": "ESP32_01"
  },
  "RosControlHub": {
    "ServerUrl": "http://localhost:5178",
    "AutoReconnect": true,
    "ReconnectIntervalSeconds": 5
  }
}
```

---

## 🚀 사용 방법

### 방법 1: OPC UA만 사용
```csharp
// Program.cs
builder.Services.AddDeviceConnector(builder.Configuration);

// 서비스에서 사용
public class MyService
{
    private readonly IOpcUaClientService _opcService;

    public MyService(IOpcUaClientService opcService)
    {
        _opcService = opcService;
    }

    public async Task RunAsync()
    {
        await _opcService.ConnectAsync();
        
        var data = await _opcService.ReadDataAsync();
        Console.WriteLine($"Position: {data?.PositionX}");
        
        await _opcService.WriteCommandAsync("ESP32_01", "REG_STATUS", 1);
    }
}
```

### 방법 2: ROS_ControlHub 통합 사용
```csharp
// Program.cs
builder.Services.AddDeviceConnectorWithRosHub(builder.Configuration);

// 서비스에서 사용
public class MyService
{
    private readonly IntegratedDeviceHub _hub;

    public MyService(IntegratedDeviceHub hub)
    {
        _hub = hub;
        
        // 이벤트 구독
        _hub.OpcDataChanged += (s, e) => 
            Console.WriteLine($"OPC 데이터: {e.Data}");
        
        _hub.RosStateUpdated += (s, e) => 
            Console.WriteLine($"ROS 상태: {e.DeviceStatus}");
    }

    public async Task RunAsync()
    {
        // 모든 서비스 연결
        await _hub.ConnectAllAsync();
        
        // 디바이스 제어
        await _hub.StartDeviceAsync("ESP32_01");
        
        // ROS_ControlHub로 명령 전송
        var result = await _hub.SendRosCommandAsync("ESP32_01", "start");
    }
}
```

### 방법 3: ROS_ControlHub 클라이언트만 사용
```csharp
// Program.cs
builder.Services.AddRosControlHubClient(builder.Configuration);

// 서비스에서 사용
public class MyService
{
    private readonly IRosControlHubClient _rosClient;

    public MyService(IRosControlHubClient rosClient)
    {
        _rosClient = rosClient;
        
        _rosClient.SystemStateUpdated += (s, e) =>
        {
            Console.WriteLine($"디바이스: {e.DeviceName}");
            Console.WriteLine($"상태: {e.DeviceStatus}");
            Console.WriteLine($"OPC 연결: {e.OpcConnected}");
        };
    }

    public async Task RunAsync()
    {
        await _rosClient.ConnectAsync();
        await _rosClient.JoinStateGroupAsync();
        
        // gRPC로 명령 전송
        var result = await _rosClient.SetDeviceStateAsync("ESP32_01", "start");
    }
}
```

---

## 📁 프로젝트 구조

```
DeviceConnector/
├── Interfaces/
│   ├── IOpcUaClientService.cs       # OPC UA 서비스 인터페이스
│   ├── IRoboDkOpcUaService.cs       # RoboDK OPC UA 서비스 인터페이스 ⭐
│   └── IRosControlHubClient.cs      # ROS_ControlHub 클라이언트 인터페이스
├── Models/
│   ├── ESP32Data.cs                 # ESP32 데이터 모델
│   ├── ConnectionStatus.cs          # 연결 상태 모델
│   ├── OpcUaConnectionInfo.cs       # OPC UA 설정
│   ├── RoboDkConnectionInfo.cs      # RoboDK 연결 설정 + RobotJointData ⭐
│   └── RosControlHubConfig.cs       # ROS_ControlHub 설정
├── Services/
│   ├── OpcUaClientService.cs        # OPC UA 클라이언트 구현
│   ├── RoboDkOpcUaService.cs        # RoboDK OPC UA Method 호출 ⭐
│   ├── RosControlHubClient.cs       # ROS_ControlHub 클라이언트 구현
│   ├── RosCompatibleOpcUaAdapter.cs # ROS_CODE 호환 어댑터
│   └── IntegratedDeviceHub.cs       # 통합 허브 서비스
├── Events/
│   └── DataChangedEventArgs.cs      # 이벤트 클래스 (RobotJointChangedEventArgs 포함)
├── Extensions/
│   └── ServiceCollectionExtensions.cs # DI 확장 메서드 (AddRoboDkOpcUaService 포함)
├── Protos/
│   └── control.proto                # ROS_ControlHub gRPC 정의
└── DeviceConnector.csproj
```

---

## 🔗 ROS_ControlHub 연동

### 통신 프로토콜
| 프로토콜 | 용도 | 엔드포인트 |
|----------|------|------------|
| gRPC | 디바이스 제어 명령 | `http://localhost:5178` |
| SignalR | 실시간 상태 수신 | `/hubs/state` |

### gRPC 서비스 (control.proto)
```protobuf
service ControlService {
  rpc SetDeviceState (DeviceCommand) returns (DeviceResult);
  rpc SetAllDevicesState (GlobalCommand) returns (GlobalResult);
  rpc MoveAgv (AgvMoveCommand) returns (DeviceResult);
}
```

### SignalR 이벤트
- `SystemStateUpdated` - 시스템 상태 업데이트 수신
- `JoinGroup` / `LeaveGroup` - 상태 그룹 참가/퇴장

---

## ⚙️ KEPServerEX 설정

### 채널/디바이스 구조
```
ModbusTCP (Channel)
└── ESP32_01 (Device)
    ├── REG_POS_X_LOW   (40001) - Position X 하위 16비트
    ├── REG_POS_X_HIGH  (40002) - Position X 상위 16비트
    ├── REG_SPEED       (40007) - 속도
    └── REG_STATUS      (40008) - 상태
```

### OPC UA Node ID 형식
```
ns=2;s=ModbusTCP.ESP32_01.REG_STATUS
```

---

## 📊 데이터 흐름

```
[ESP32] ──Modbus TCP──▶ [KEPServerEX] ──OPC UA──▶ [DeviceConnector]
                                                        │
                                                        ├──▶ 로컬 앱
                                                        │
                                                        └──gRPC/SignalR──▶ [ROS_ControlHub]
                                                                                │
                                                                                ├──▶ Unity
                                                                                └──▶ Web UI
```

---

## 📄 라이선스

MIT License
