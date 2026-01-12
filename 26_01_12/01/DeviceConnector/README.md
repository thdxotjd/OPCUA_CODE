# DeviceConnector

ESP32 ModbusTCP - OPC UA - gRPC 통신 클래스 라이브러리  
**ROS_ControlHub 통합 지원**

---

## 📋 개요

DeviceConnector는 산업용 장비 통신을 위한 .NET 8 클래스 라이브러리입니다.

### 주요 기능
1. **OPC UA 통신** - KEPServerEX를 통한 ESP32/PLC 데이터 읽기/쓰기
2. **ROS_ControlHub 통합** - gRPC/SignalR을 통한 양방향 통신
3. **실시간 구독** - OPC UA Subscription을 통한 데이터 변경 감지
4. **자동 재연결** - 연결 끊김 시 자동 복구

---

## 🏗️ 아키텍처

```
┌──────────────────────────────────────────────────────────────┐
│                        DeviceConnector                        │
├───────────────────────────┬──────────────────────────────────┤
│      OPC UA 클라이언트     │      ROS_ControlHub 클라이언트    │
│   (OpcUaClientService)    │     (RosControlHubClient)        │
├───────────────────────────┴──────────────────────────────────┤
│                   IntegratedDeviceHub                        │
│            (통합 관리 + 상태 동기화)                           │
└───────────────────────────┬──────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  KEPServerEX  │   │ ROS_ControlHub│   │   SignalR     │
│  (OPC UA)     │   │   (gRPC)      │   │   (실시간)    │
└───────┬───────┘   └───────────────┘   └───────────────┘
        │
        ▼
┌───────────────┐
│ ESP32/PLC     │
│ (Modbus TCP)  │
└───────────────┘
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
│   └── IRosControlHubClient.cs      # ROS_ControlHub 클라이언트 인터페이스
├── Models/
│   ├── ESP32Data.cs                 # ESP32 데이터 모델
│   ├── ConnectionStatus.cs          # 연결 상태 모델
│   ├── OpcUaConnectionInfo.cs       # OPC UA 설정
│   └── RosControlHubConfig.cs       # ROS_ControlHub 설정
├── Services/
│   ├── OpcUaClientService.cs        # OPC UA 클라이언트 구현
│   ├── RosControlHubClient.cs       # ROS_ControlHub 클라이언트 구현
│   ├── RosCompatibleOpcUaAdapter.cs # ROS_CODE 호환 어댑터
│   └── IntegratedDeviceHub.cs       # 통합 허브 서비스
├── Events/
│   └── DataChangedEventArgs.cs      # 이벤트 클래스
├── Extensions/
│   └── ServiceCollectionExtensions.cs # DI 확장 메서드
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
