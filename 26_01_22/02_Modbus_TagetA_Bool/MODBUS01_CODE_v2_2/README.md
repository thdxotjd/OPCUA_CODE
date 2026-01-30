# MODBUS01_CODE v2.2

ESP32 ModbusTCP - KEPServerEX OPC UA 클라이언트 라이브러리

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
MODBUS01_CODE_v2_2/
├── DeviceConnector.sln                 # 솔루션 파일
│
├── DeviceConnector/                    # 클래스 라이브러리
│   ├── Models/
│   │   ├── ESP32Data.cs               # ESP32 데이터 모델
│   │   ├── ConnectionStatus.cs        # 연결 상태
│   │   └── OpcUaConnectionInfo.cs     # 연결 설정 + 태그 설정
│   │
│   ├── Events/
│   │   └── DataChangedEventArgs.cs    # 이벤트 정의
│   │
│   ├── Interfaces/
│   │   └── IOpcUaClientService.cs     # 서비스 인터페이스
│   │
│   ├── Services/
│   │   └── OpcUaClientService.cs      # OPC UA 클라이언트 구현
│   │
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs  # DI 확장
│
└── DeviceConnector.Test/               # 테스트 콘솔
    └── Program.cs
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
