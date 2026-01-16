# OPC UA 가짜 데이터 쓰기 서비스

## 📋 개요

KEPServerEX OPC UA 서버에 연결하여 **가짜 테스트 데이터를 쓰는** 서비스입니다.
gRPC 개발자가 실제 장비 없이도 데이터 흐름을 테스트할 수 있습니다.

---

## 🔧 데이터 구조 규칙 (2026-02-28까지 유지)

| 필드 | 설명 | 예시 |
|------|------|------|
| **DeviceName** | 장비 이름 | `ESP32_01`, `PLC_01` |
| **ChannelName** | KEPServerEX 채널명 | `ModbusTCP`, `MitsubishiSerial` |
| **Tags** | 태그 딕셔너리 | `{"Speed": 100}` |

### OPC UA Node ID 형식
```
ns=2;s={ChannelName}.{DeviceName}.{TagName}
```
예: `ns=2;s=ModbusTCP.ESP32_01.Speed`

---

## 📦 제공 장비 및 태그

### ESP32_01 (ModbusTCP)
| 태그 | 타입 | 범위 | 설명 |
|------|------|------|------|
| Connected | bool | - | 연결 상태 |
| Running | bool | - | 동작 상태 |
| Speed | short | 0~200 | 속도 |
| PositionX | float | -100~100 | X 위치 |
| PositionY | float | -100~100 | Y 위치 |
| PositionZ | float | 0~50 | Z 위치 |
| Temperature | float | 20~80 | 온도 |
| ErrorCode | short | - | 에러 코드 |
| Status | short | 0~3 | 0=Idle, 1=Running, 2=Pause, 3=Error |

### PLC_01 (MitsubishiSerial)
| 태그 | 타입 | 범위 | 설명 |
|------|------|------|------|
| D100 | short | 0~9999 | 데이터 레지스터 |
| D101 | short | 0~9999 | 데이터 레지스터 |
| D102 | short | 0~9999 | 데이터 레지스터 |
| D200 | short | - | 데이터 레지스터 |
| M0, M1, M100 | bool | - | 내부 릴레이 |
| Y0, Y1 | bool | - | 출력 |

### AGV_01 (ModbusTCP)
| 태그 | 타입 | 범위 | 설명 |
|------|------|------|------|
| Connected | bool | - | 연결 상태 |
| Running | bool | - | 이동 중 |
| BatteryLevel | float | 0~100 | 배터리 % |
| CurrentX | float | -500~500 | 현재 X |
| CurrentY | float | -500~500 | 현재 Y |
| TargetX | float | - | 목표 X |
| TargetY | float | - | 목표 Y |
| Speed | float | 0~50 | 이동 속도 |
| Status | short | 0~3 | 0=Idle, 1=Moving, 2=Charging, 3=Error |

### ROBOT_01 (RobotController)
| 태그 | 타입 | 범위 | 설명 |
|------|------|------|------|
| Connected | bool | - | 연결 상태 |
| Running | bool | - | 동작 중 |
| Joint1~6 | float | -180~180 | 조인트 각도 |
| GripperState | bool | - | 그리퍼 열림/닫힘 |
| ProgramRunning | bool | - | 프로그램 실행 중 |
| ErrorCode | short | - | 에러 코드 |

---

## 🚀 사용 방법

### 1. 빌드 및 실행
```bash
dotnet restore
dotnet build
dotnet run
```

### 2. 코드에서 사용
```csharp
// OPC UA 서버에 연결
var service = new FakeDataWriterService("opc.tcp://localhost:49320");

// 단일 태그 쓰기
await service.WriteTagAsync("ESP32_01", "Speed", (short)150);

// 여러 태그 쓰기
await service.WriteTagsAsync("ESP32_01", new Dictionary<string, object>
{
    ["Running"] = true,
    ["Speed"] = (short)100,
    ["Temperature"] = 35.5f
});

// 초기값 쓰기
await service.WriteInitialValuesAsync();

// 랜덤 데이터 쓰기
await service.WriteRandomDataAsync("ESP32_01");

// 자동 업데이트 (1초 간격)
service.StartAutoUpdate(1000);

// 자동 업데이트 중지
service.StopAutoUpdate();
```

### 3. 시나리오 테스트
```csharp
// ESP32 시작
await service.SimulateEsp32StartAsync();

// ESP32 정지
await service.SimulateEsp32StopAsync();

// ESP32 에러 발생
await service.SimulateEsp32ErrorAsync(101);

// AGV 이동 (목표 좌표로)
await service.SimulateAgvMoveAsync(50.0f, 30.0f);

// PLC 생산 카운트 (10개 생산)
await service.SimulatePlcProductionAsync(10);
```

---

## ⚙️ KEPServerEX 설정 필요사항

### 1. OPC UA 활성화
- KEPServerEX → OPC UA Configuration → Server Endpoints
- 엔드포인트 활성화 (기본: `opc.tcp://localhost:49320`)

### 2. 채널/디바이스 생성
KEPServerEX에서 다음 구조로 채널과 디바이스를 생성해야 합니다:

```
ModbusTCP (Channel)
├── ESP32_01 (Device)
│   ├── Connected
│   ├── Running
│   ├── Speed
│   └── ...
└── AGV_01 (Device)
    ├── Connected
    ├── BatteryLevel
    └── ...

MitsubishiSerial (Channel)
└── PLC_01 (Device)
    ├── D100
    ├── M0
    └── ...
```

### 3. 태그 권한
- 태그가 **쓰기 가능(Writable)**으로 설정되어 있어야 합니다.

---

## 🔗 ROS_ControlHub 연동

이 서비스로 쓴 데이터는 ROS_ControlHub에서 실시간으로 읽을 수 있습니다:

```
[이 서비스] ──Write──▶ [KEPServerEX] ◀──Read── [ROS_ControlHub]
                           │
                           ▼
                    [gRPC/SignalR 클라이언트]
```

---

## 📞 문의

gRPC 개발 관련 문의는 담당자에게 연락 바랍니다.
