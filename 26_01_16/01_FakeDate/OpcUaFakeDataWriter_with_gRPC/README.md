# OPC UA 가짜 데이터 + ROS_ControlHub gRPC 통신

## 📋 개요

1. **FakeDataWriterService**: KEPServerEX OPC UA에 가짜 데이터 쓰기
2. **RosControlHubClient**: ROS_ControlHub와 gRPC 통신

---

## 🔧 데이터 구조 규칙 (2026-02-28까지 유지)

| 필드 | 설명 | 예시 |
|------|------|------|
| **DeviceName** | 장비 이름 | `ESP32_01`, `PLC_01` |
| **ChannelName** | KEPServerEX 채널명 | `ModbusTCP` |
| **Tags** | 태그 딕셔너리 | `{"Speed": 100}` |

---

## 📊 전체 구조

```
┌─────────────────────────────────────────────────────────────────────┐
│                           이 프로젝트                                │
│                                                                     │
│  ┌──────────────────┐                        ┌───────────────────┐ │
│  │ FakeDataWriter   │───Write───▶ KEPServerEX│                   │ │
│  │ (OPC UA Client)  │            (OPC UA)    │                   │ │
│  └──────────────────┘                        │                   │ │
│                                              │   ROS_ControlHub  │ │
│  ┌──────────────────┐                        │   (gRPC Server)   │ │
│  │ RosControlHub    │◀──gRPC/REST────────────│                   │ │
│  │ Client           │                        │                   │ │
│  └──────────────────┘                        └───────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 사용 방법

### 1. 빌드 및 실행
```bash
dotnet restore
dotnet build
dotnet run
```

### 2. 모드 선택
```
모드 선택:
1. OPC UA 가짜 데이터 쓰기 (KEPServerEX만)
2. ROS_ControlHub gRPC 클라이언트 (gRPC만)
3. 둘 다 (통합 테스트) ← 권장
```

### 3. 메뉴 예시
```
--- OPC UA (KEPServerEX에 가짜 데이터 쓰기) ---
1. 모든 장비 초기값 쓰기
2. 단일 태그 값 쓰기
3. ESP32 시작 시나리오
4. ESP32 정지 시나리오
5. 자동 업데이트 시작/중지

--- gRPC (ROS_ControlHub 통신) ---
11. [gRPC] 연결 테스트
12. [gRPC] 장비 상태 조회
13. [gRPC] 모든 장비 상태 조회
14. [gRPC] 장비 제어 - Start
15. [gRPC] 장비 제어 - Stop
16. [gRPC] AGV 이동 명령

--- 통합 테스트 ---
21. OPC UA 쓰기 → gRPC 읽기 테스트
```

---

## 📦 코드에서 사용

### OPC UA 가짜 데이터 쓰기
```csharp
var opcua = new FakeDataWriterService("opc.tcp://localhost:49320");

// 태그 쓰기
await opcua.WriteTagAsync("ESP32_01", "Speed", (short)150);

// 시나리오 실행
await opcua.SimulateEsp32StartAsync();
```

### gRPC 클라이언트
```csharp
var grpc = new RosControlHubClient("http://localhost:5178");

// 장비 상태 조회
var state = await grpc.GetDeviceStateAsync("ESP32_01");

// 장비 제어
await grpc.SetDeviceStateAsync("ESP32_01", "start");

// AGV 이동
await grpc.MoveAgvAsync("AGV_01", 50.0, 30.0);
```

---

## ⚙️ 필요 조건

### 1. KEPServerEX
- OPC UA 엔드포인트 활성화 (기본: `opc.tcp://localhost:49320`)
- 채널/디바이스/태그 생성

### 2. ROS_ControlHub
- gRPC 서버 실행 중 (기본: `http://localhost:5178`)

---

## 🔗 gRPC Proto 정의

```protobuf
service ControlService {
  rpc SetDeviceState (DeviceCommand) returns (DeviceResult);
  rpc SetAllDevicesState (GlobalCommand) returns (GlobalResult);
  rpc MoveAgv (AgvMoveCommand) returns (DeviceResult);
}
```

---

## 📞 문의

gRPC 개발 관련 문의는 담당자에게 연락 바랍니다.
