# DeviceConnector (MODBUS_CODE)

ESP32 ModbusTCP - KEPServerEX OPC UA 클라이언트 라이브러리

## KEPServerEX Tag 구성

| Tag Name | Address | Data Type | Access | 설명 |
|----------|---------|-----------|--------|------|
| POS_X | 40001 | Float | Read Only | X 좌표 (m) |
| POS_Y | 40003 | Float | Read Only | Y 좌표 (m) |
| POS_T | 40005 | Float | Read Only | 방향각 (deg) |
| TargetA | 40007.0 | Boolean | Write | 목표 도달 플래그 |
| Control | 40100.20H | String | Write | 제어 명령 |
| State | 40200.20H | String | **Read/Write** | 상태 문자열 |

## 솔루션 구조

```
DeviceConnector.sln
├── DeviceConnector/              # 클래스 라이브러리
│   ├── Models/
│   │   ├── ESP32Data.cs          # ESP32 데이터 모델
│   │   └── OpcUaConnectionInfo.cs # 연결 설정
│   ├── Interfaces/
│   │   └── IOpcUaClientService.cs # 서비스 인터페이스
│   ├── Services/
│   │   └── OpcUaClientService.cs  # OPC UA 클라이언트 구현
│   ├── Events/
│   │   └── DataChangedEventArgs.cs # 이벤트 클래스
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs # DI 확장
│
└── DeviceConnector.Test/         # 테스트 콘솔 프로그램
    └── Program.cs                # 테스트 메인
```

## 빠른 시작

### 1. 테스트 실행

```bash
# 빌드
dotnet build

# 테스트 프로그램 실행
dotnet run --project DeviceConnector.Test
```

### 2. 테스트 메뉴

```
╔═══════════════════════════════════════╗
║           Test Menu                   ║
╠═══════════════════════════════════════╣
║  1. Read All Data                     ║
║  2. Read Position Only (POS_X/Y/T)    ║
║  3. Read State                        ║
║  4. Write TargetA (Boolean)           ║
║  5. Write Control (String)            ║
║  6. Write State (String)              ║
║  7. Write Multiple Tags               ║
║  8. Start Subscription (Real-time)    ║
║  9. Stop Subscription                 ║
║  0. Exit                              ║
╚═══════════════════════════════════════╝
```

## 사용 방법

### 1. 기본 사용

```csharp
// 연결 설정
var connectionInfo = new OpcUaConnectionInfo
{
    EndpointUrl = "opc.tcp://192.168.0.19:49320",
    PublishingIntervalMs = 100
};

// 디바이스 설정 (DeviceName, ChannelName, Tags 구조)
var deviceConfig = new DeviceTagConfig
{
    DeviceId = "ESP32_01",
    ChannelName = "ModbusTCP",
    DeviceName = "ESP32_01"
};

// 클라이언트 생성 및 연결
using var client = new OpcUaClientService(connectionInfo);
client.AddDeviceConfig(deviceConfig);
await client.ConnectAsync();

// 위치 데이터 읽기 (Read Only)
var position = await client.ReadPositionAsync("ESP32_01");
Console.WriteLine($"Position: X={position?.PosX}, Y={position?.PosY}, T={position?.PosT}");

// 제어 명령 쓰기 (Write)
await client.WriteControlAsync("ESP32_01", "MOVE");
await client.WriteStateAsync("ESP32_01", "MOVING");
await client.WriteTargetAAsync("ESP32_01", true);
```

### 2. DI 컨테이너 사용 (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddDeviceConnector(options =>
{
    options.EndpointUrl = "opc.tcp://192.168.0.19:49320";
    options.PublishingIntervalMs = 100;
});

// appsettings.json 사용
builder.Services.AddDeviceConnector(builder.Configuration, "OpcUa");
```

**appsettings.json:**
```json
{
  "OpcUa": {
    "EndpointUrl": "opc.tcp://192.168.0.19:49320",
    "PublishingIntervalMs": 100,
    "AutoReconnect": true
  }
}
```

### 3. 이벤트 기반 데이터 수신 (구독)

```csharp
client.DataChanged += (sender, e) =>
{
    Console.WriteLine($"[{e.DeviceId}] {e.Data}");
    Console.WriteLine($"  Changed Tags: {string.Join(", ", e.ChangedTags)}");
};

client.WriteCompleted += (sender, e) =>
{
    Console.WriteLine($"Write {e.TagName} = {e.Value} -> {(e.Success ? "OK" : e.ErrorMessage)}");
};

await client.StartSubscriptionAsync("ESP32_01");
```

### 4. 여러 태그 동시 쓰기

```csharp
var results = await client.WriteMultipleAsync("ESP32_01", new Dictionary<string, object>
{
    { ESP32Tags.CONTROL, "MOVE" },
    { ESP32Tags.STATE, "MOVING" },
    { ESP32Tags.TARGET_A, true }
});

foreach (var result in results)
{
    Console.WriteLine($"{result.Key}: {(result.Value ? "OK" : "Failed")}");
}
```

## ESP32 Modbus 레지스터 맵

| Modbus 주소 | 레지스터 인덱스 | 타입 | 태그 |
|-------------|----------------|------|------|
| 40001-40002 | 0-1 | Float | POS_X |
| 40003-40004 | 2-3 | Float | POS_Y |
| 40005-40006 | 4-5 | Float | POS_T |
| 40007.0 | 6 (bit 0) | Boolean | TargetA |
| 40100.20H | 99-108 | String (20 bytes) | Control |
| 40200.20H | 199-208 | String (20 bytes) | State |

## OPC UA NodeId 형식

KEPServerEX에서 태그의 NodeId 형식:
```
ns=2;s=ChannelName.DeviceName.TagName
```

예시:
- `ns=2;s=ModbusTCP.ESP32_01.POS_X`
- `ns=2;s=ModbusTCP.ESP32_01.Control`
- `ns=2;s=ModbusTCP.ESP32_01.State`

## 빌드

```bash
dotnet build
```

## 의존성

- .NET 8.0
- OPCFoundation.NetStandard.Opc.Ua.Client 1.5.374.126
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2
- Microsoft.Extensions.Logging.Abstractions 8.0.2
