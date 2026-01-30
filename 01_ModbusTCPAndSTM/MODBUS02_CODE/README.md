# MODBUS02_CODE v2.2

ESP32 ModbusTCP 및 STM_yolo - KEPServerEX OPC UA 클라이언트 라이브러리

## 📌 지원 디바이스

1. **ESP32 ModbusTCP** - 기존
2. **STM_yolo** - 신규 추가 (Channel1_opcua > ModbusTCP > STM > Stm_yolo)

---

## 🆕 STM_yolo 태그 구성

### NodeId 형식
```
ns=2;s=STM.Stm_yolo.TagName
```
예: `ns=2;s=STM.Stm_yolo.TargetState`

### Write Tags (Target) - OPC UA → PLC

| Tag Name | NodeId | Data Type | 설명 |
|----------|--------|-----------|------|
| TargetState | ns=2;s=STM.Stm_yolo.TargetState | LLong | 목표 상태 |
| TargetSpeedMain | ns=2;s=STM.Stm_yolo.TargetSpeedMain | LLong | 메인 컨베이어 속도 |
| TargetSpeedSort | ns=2;s=STM.Stm_yolo.TargetSpeedSort | LLong | 분류 컨베이어 속도 |
| TargetSpeedLoad | ns=2;s=STM.Stm_yolo.TargetSpeedLoad | LLong | 적재 컨베이어 속도 |
| AgvSortArrived | ns=2;s=STM.Stm_yolo.AgvSortArrived | Boolean | AGV 분류 도착 |
| AgvSortDeparted | ns=2;s=STM.Stm_yolo.AgvSortDeparted | Boolean | AGV 분류 출발 |
| AgvLoadArrived | ns=2;s=STM.Stm_yolo.AgvLoadArrived | Boolean | AGV 적재 도착 |
| AgvLoadDeparted | ns=2;s=STM.Stm_yolo.AgvLoadDeparted | Boolean | AGV 적재 출발 |

### Read Tags (Current) - PLC → OPC UA

| Tag Name | NodeId | Data Type | 설명 |
|----------|--------|-----------|------|
| CurrentState | ns=2;s=STM.Stm_yolo.CurrentState | LLong | 현재 상태 |
| CurrentSpeedMain | ns=2;s=STM.Stm_yolo.CurrentSpeedMain | LLong | 현재 메인 속도 |
| CurrentSpeedSort | ns=2;s=STM.Stm_yolo.CurrentSpeedSort | LLong | 현재 분류 속도 |
| CurrentSpeedLoad | ns=2;s=STM.Stm_yolo.CurrentSpeedLoad | LLong | 현재 적재 속도 |
| CurrentFloor | ns=2;s=STM.Stm_yolo.CurrentFloor | LLong | 현재 층 |
| IsLiftMoving | ns=2;s=STM.Stm_yolo.IsLiftMoving | Boolean | 리프트 동작 중 |
| IsRobotWorking | ns=2;s=STM.Stm_yolo.IsRobotWorking | Boolean | 로봇 작업 중 |
| IsRobotDone | ns=2;s=STM.Stm_yolo.IsRobotDone | Boolean | 로봇 작업 완료 |

---

## 🚀 STM_yolo 사용 예제

```csharp
// 연결 설정
var connectionInfo = new OpcUaConnectionInfo
{
    EndpointUrl = "opc.tcp://127.0.0.1:49320",
    AutoReconnect = true
};

// 태그 설정
var tagConfig = new STMYoloTagConfig
{
    DeviceId = "STM_yolo",
    ChannelName = "Channel1_opcua",
    DeviceName = "STM",
    TagGroupName = "Stm_yolo",
    NamespaceIndex = 2
};

// 클라이언트 생성 및 연결
var client = new STMYoloClientService(connectionInfo, tagConfig);
await client.ConnectAsync();

// 데이터 읽기
var data = await client.ReadAllDataAsync();
Console.WriteLine($"CurrentState: {data.CurrentState}");
Console.WriteLine($"CurrentFloor: {data.CurrentFloor}");

// 데이터 쓰기
await client.WriteTargetStateAsync(1);
await client.WriteAllSpeedsAsync(100, 80, 60);  // Main, Sort, Load
await client.WriteAgvSortArrivedAsync(true);

// 구독 (실시간 데이터 변경 감지)
client.DataChanged += (s, e) => Console.WriteLine($"{e.ChangedTagName}: {e.NewValue}");
await client.StartAllSubscriptionsAsync();
```

---

## ⚠️ v2.2 주요 변경사항

### TargetA 태그 주소 변경

| 항목 | v2.1 (이전) | v2.2 (현재) |
|------|-------------|-------------|
| Address | `40007.0` | `00007` |
| Type | Holding Register Bit | **Coil** |
| Function Code | FC03/FC06 | **FC05 (Write Single Coil)** |

**변경 이유:**
- Holding Register 비트 주소(`40007.0`)로 Boolean Write 시 실패 문제
- Coil 주소(`00007`)는 Modbus FC05를 사용하여 안정적인 Boolean Write 가능

---

## 📁 프로젝트 구조

```
MODBUS02_CODE/
├── DeviceConnector.sln                 # 솔루션 파일
├── README.md                           # 프로젝트 개요
├── GRPC_DEVELOPER_GUIDE.md            # gRPC 개발자 가이드
│
└── DeviceConnector/                    # 클래스 라이브러리
    ├── Models/
    │   ├── ESP32Data.cs               # ESP32 데이터 모델
    │   ├── STMYoloData.cs             # STM_yolo 데이터 모델
    │   ├── ConnectionStatus.cs        # 연결 상태
    │   └── OpcUaConnectionInfo.cs     # 연결 설정 + 태그 설정
    │
    ├── Events/
    │   ├── DataChangedEventArgs.cs    # ESP32 이벤트
    │   └── STMYoloDataChangedEventArgs.cs  # STM_yolo 이벤트
    │
    ├── Interfaces/
    │   ├── IOpcUaClientService.cs     # ESP32 서비스 인터페이스
    │   └── ISTMYoloClientService.cs   # STM_yolo 서비스 인터페이스
    │
    ├── Services/
    │   ├── OpcUaClientService.cs      # ESP32 OPC UA 클라이언트
    │   └── STMYoloClientService.cs    # STM_yolo OPC UA 클라이언트
    │
    └── Extensions/
        └── ServiceCollectionExtensions.cs  # DI 확장
```

---

## 🏷️ KEPServerEX 태그 설정

### v2.2 태그 구성

| Tag Name | Address | Data Type | Direction | 설명 |
|----------|---------|-----------|-----------|------|
| POS_X | 40001 | Float | Read | X 좌표 (ESP32 → OPC) |
| POS_Y | 40003 | Float | Read | Y 좌표 (ESP32 → OPC) |
| POS_T | 40005 | Float | Read | 각도 (ESP32 → OPC) |
| **TargetA** | **00007** | **Boolean** | **Write** | 목표 A 플래그 **(Coil)** |
| Control | 40100.20H | String | Write | 제어 명령 |
| State | 40200.20H | String | Write | 상태 정보 |

### KEPServerEX 설정 방법

1. **TargetA 태그 수정:**
   - Tag Properties 열기
   - Address: `00007` 입력 (Coil 주소)
   - Data Type: `Boolean` 선택
   - Client Access: `Read/Write` 설정

2. **Modbus 설정:**
   - Modbus Byte Order: Enable (Big-Endian)
   - First Word Low: Disable

---

## 🚀 사용 방법

### 1. 연결 및 설정

```csharp
var connectionInfo = new OpcUaConnectionInfo
{
    EndpointUrl = "opc.tcp://127.0.0.1:49320",
    AutoReconnect = true
};

var deviceConfig = new DeviceTagConfig
{
    DeviceId = "ESP32_01",
    ChannelName = "ModbusTCP",
    DeviceName = "ESP32_01",
    Tags = new DeviceTagNames
    {
        PosX = "POS_X",
        PosY = "POS_Y",
        PosTheta = "POS_T",
        TargetA = "TargetA",   // Coil 00007
        Control = "Control",
        State = "State"
    }
};

var client = new OpcUaClientService(connectionInfo);
client.AddDeviceConfig(deviceConfig);
await client.ConnectAsync();
```

### 2. 데이터 읽기

```csharp
var data = await client.ReadDeviceDataAsync("ESP32_01");
Console.WriteLine($"Position: ({data.PosX}, {data.PosY}, {data.PosTheta})");
Console.WriteLine($"TargetA: {data.TargetA}");
```

### 3. TargetA 쓰기 (Coil)

```csharp
// v2.2: Coil 주소(00007)로 FC05 Write Single Coil 사용
bool result = await client.WriteTargetAAsync("ESP32_01", true);
Console.WriteLine(result ? "Success" : "Failed");
```

### 4. 구독

```csharp
client.DataChanged += (sender, e) =>
{
    Console.WriteLine($"Data changed: {e.Data}");
};

await client.StartSubscriptionAsync("ESP32_01");
```

---

## 🔧 ESP32 Modbus Slave 설정

ESP32에서 Coil Write를 지원하려면 **FC05 핸들러** 추가 필요:

```cpp
case 0x05: {  // Write Single Coil
    uint16_t coilAddr = (request[8] << 8) | request[9];
    uint16_t coilValue = (request[10] << 8) | request[11];
    
    if (coilAddr == 7) {  // Coil 7 = TargetA
        bool value = (coilValue == 0xFF00);  // 0xFF00=ON, 0x0000=OFF
        // TargetA 값 저장
        targetA = value;
        
        // Echo response
        memcpy(response, request, 12);
        responseLen = 12;
    }
    break;
}
```

---

## 🔄 OPC UA NodeId 형식

```
ns=2;s=ChannelName.DeviceName.TagName
```

예시:
- `ns=2;s=ModbusTCP.ESP32_01.POS_X`
- `ns=2;s=ModbusTCP.ESP32_01.TargetA`

---

## 📝 버전 히스토리

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| **v2.2** | **2026-01-22** | **TargetA 주소 Coil(00007)로 변경, FC05 지원** |
| v2.1 | 2026-01-22 | TargetA, Control, State Write 기능 추가 |
| v2.0 | 2026-01-20 | 멀티 디바이스 지원, 이벤트 시스템 개선 |
| v1.0 | 2026-01-07 | 초기 버전 |

---

## ⚙️ 빌드 및 실행

```bash
# 솔루션 빌드
dotnet build DeviceConnector.sln

# 테스트 프로그램 실행
dotnet run --project DeviceConnector.Test

# 릴리스 빌드
dotnet build -c Release
```
