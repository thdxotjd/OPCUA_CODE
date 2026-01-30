#  OPC UA - ASP.NET 실시간 데이터 연동 및 제어 프로젝트

본 프로젝트는 산업용 표준 프로토콜인 **OPC UA**를 활용하여 하드웨어 데이터를 **ASP.NET Core** 애플리케이션과 연동하고, 시스템을 실시간으로 모니터링 및 제어하는 통합 솔루션을 구축하는 것을 목표로 합니다.

<br>

## 📅 개발 히스토리

### **2026-01-12: OPC UA - ASP.NET 연결 프레임워크 구축**
### **2026-01-13: OPCUA RobotDK 데이터 전송 (테스트 용도)**
### **2026-01-15: OPCUA - gRPC 연동 테스트**
### **2026-01-16: OPCUA - gRPC 데이터 전송** 
### **2026-01-19: PLC MXComponent OPCUA 서버 및 태그 생성** 
### **2026-01-19: PLC MXComponent OPCUA 서버 및 태그 생성**
### **2026-01-21: ModbusTCP 클라이언트 라이브러리**
### **2026-01-22: ModbusTCP TargetA 타입 변경**
### **2026-01-26: ModbusTCP, STM OPCUA 클라이언트 라이브러리**

<br>

## 🏗 시스템 아키텍처



* **OPC UA Server (Middleware)**: 하드웨어 레지스터를 OPC UA 노드로 매핑하여 상위 애플리케이션에 인터페이스 제공, 하드웨어 데이터를 수집 및 제어.
* **ASP.NET Core Application**: 수집된 데이터를 가공하여 인터페이스 제공(Web)과 모니터링 기능 제공(Unity).

<br>

## 🛠 주요 기능
* **실시간 모니터링**: 100ms 단위의 데이터 구독을 통해 지연 없는 상태 감시 가능.
* **범용 쓰기 인터페이스**: 다양한 데이터 타입(ushort, float 등)에 대응하는 WriteTagAsync<T> 메서드 제공.
* **자동 복구 시스템**: 통신 단절 시 지정된 간격으로 세션을 재생성하는 자동 재연결(Auto-Reconnect) 로직 내장.
* **데이터 무결성 처리**: 16비트 레지스터 조합을 통해 32비트 Float 데이터를 오차 없이 복원.

<br>

## 💻 사용 예시 (Control Logic)

```csharp
// 장치 속도 제어 예시
public async Task UpdateDeviceSpeed(ushort newSpeed)
{
    // WriteSpeedAsync를 호출하여 OPC UA 서버를 통해 장치에 속도 명령 전달
    bool isSuccess = await _opcUaService.WriteSpeedAsync(newSpeed);
    
    if (isSuccess) 
        Console.WriteLine("명령이 성공적으로 전달되었습니다.");
}
⚠️ 라이선스 및 운영 참고사항
라이선스 제약: 사용 중인 서버 솔루션의 데모 라이선스 만료(2시간 제한) 시 런타임 재시작이 필요합니다.

접근 권한: 제어 기능을 사용하기 위해서는 서버 측 태그 설정에서 **'Client Access'**가 Read/Write로 설정되어 있어야 합니다.
