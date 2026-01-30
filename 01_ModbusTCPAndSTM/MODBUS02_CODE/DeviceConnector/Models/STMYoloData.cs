namespace DeviceConnector.Models;

/// <summary>
/// STM_yolo 디바이스 데이터 모델
/// KEPServerEX OPC UA NodeId 형식: ns=2;s=ChannelName.DeviceName.TagName
/// 예: ns=2;s=STM.Stm_yolo.TargetState
/// 
/// ┌──────────────────────────────────────────────────────────────────────────┐
/// │ 태그 구성 (KEPServerEX OPC UA)                                           │
/// ├───────────────────┬─────────────────────────────────┬───────────┬────────┤
/// │ Tag Name          │ NodeId                          │ Data Type │ 방향   │
/// ├───────────────────┼─────────────────────────────────┼───────────┼────────┤
/// │ TargetState       │ ns=2;s=STM.Stm_yolo.TargetState │ LLong     │ Write  │
/// │ TargetSpeedMain   │ ns=2;s=STM.Stm_yolo.TargetSpeedMain │ LLong │ Write  │
/// │ TargetSpeedSort   │ ns=2;s=STM.Stm_yolo.TargetSpeedSort │ LLong │ Write  │
/// │ TargetSpeedLoad   │ ns=2;s=STM.Stm_yolo.TargetSpeedLoad │ LLong │ Write  │
/// │ AgvSortArrived    │ ns=2;s=STM.Stm_yolo.AgvSortArrived  │ Boolean│ Write  │
/// │ AgvSortDeparted   │ ns=2;s=STM.Stm_yolo.AgvSortDeparted │ Boolean│ Write  │
/// │ AgvLoadArrived    │ ns=2;s=STM.Stm_yolo.AgvLoadArrived  │ Boolean│ Write  │
/// │ AgvLoadDeparted   │ ns=2;s=STM.Stm_yolo.AgvLoadDeparted │ Boolean│ Write  │
/// │ CurrentState      │ ns=2;s=STM.Stm_yolo.CurrentState    │ LLong  │ Read   │
/// │ CurrentSpeedMain  │ ns=2;s=STM.Stm_yolo.CurrentSpeedMain│ LLong  │ Read   │
/// │ CurrentSpeedSort  │ ns=2;s=STM.Stm_yolo.CurrentSpeedSort│ LLong  │ Read   │
/// │ CurrentSpeedLoad  │ ns=2;s=STM.Stm_yolo.CurrentSpeedLoad│ LLong  │ Read   │
/// │ CurrentFloor      │ ns=2;s=STM.Stm_yolo.CurrentFloor    │ LLong  │ Read   │
/// │ IsLiftMoving      │ ns=2;s=STM.Stm_yolo.IsLiftMoving    │ Boolean│ Read   │
/// │ IsRobotWorking    │ ns=2;s=STM.Stm_yolo.IsRobotWorking  │ Boolean│ Read   │
/// │ IsRobotDone       │ ns=2;s=STM.Stm_yolo.IsRobotDone     │ Boolean│ Read   │
/// └───────────────────┴─────────────────────────────────┴───────────┴────────┘
/// </summary>
public class STMYoloData
{
    #region 식별 정보

    /// <summary>디바이스 ID (예: "STM_yolo")</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>KEPServerEX 채널명 (예: "Channel1_opcua")</summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>KEPServerEX 디바이스명 (예: "STM")</summary>
    public string DeviceName { get; set; } = string.Empty;

    #endregion

    #region Target (Write - OPC UA → PLC/Device)

    /// <summary>목표 상태 - ns=2;i=40001 (LLong)</summary>
    public long TargetState { get; set; }

    /// <summary>목표 메인 컨베이어 속도 - ns=2;i=40002 (LLong)</summary>
    public long TargetSpeedMain { get; set; }

    /// <summary>목표 분류 컨베이어 속도 - ns=2;i=40003 (LLong)</summary>
    public long TargetSpeedSort { get; set; }

    /// <summary>목표 적재 컨베이어 속도 - ns=2;i=40004 (LLong)</summary>
    public long TargetSpeedLoad { get; set; }

    /// <summary>AGV 분류 도착 플래그 - ns=2;i=40005 (Boolean)</summary>
    public bool AgvSortArrived { get; set; }

    /// <summary>AGV 분류 출발 플래그 - ns=2;i=40006 (Boolean)</summary>
    public bool AgvSortDeparted { get; set; }

    /// <summary>AGV 적재 도착 플래그 - ns=2;i=40007 (Boolean)</summary>
    public bool AgvLoadArrived { get; set; }

    /// <summary>AGV 적재 출발 플래그 - ns=2;i=40008 (Boolean)</summary>
    public bool AgvLoadDeparted { get; set; }

    #endregion

    #region Current (Read - PLC/Device → OPC UA)

    /// <summary>현재 상태 - ns=2;i=50001 (LLong)</summary>
    public long CurrentState { get; set; }

    /// <summary>현재 메인 컨베이어 속도 - ns=2;i=50002 (LLong)</summary>
    public long CurrentSpeedMain { get; set; }

    /// <summary>현재 분류 컨베이어 속도 - ns=2;i=50003 (LLong)</summary>
    public long CurrentSpeedSort { get; set; }

    /// <summary>현재 적재 컨베이어 속도 - ns=2;i=50004 (LLong)</summary>
    public long CurrentSpeedLoad { get; set; }

    /// <summary>현재 층 - ns=2;i=50005 (LLong)</summary>
    public long CurrentFloor { get; set; }

    /// <summary>리프트 동작 중 - ns=2;i=50006 (Boolean)</summary>
    public bool IsLiftMoving { get; set; }

    /// <summary>로봇 작업 중 - ns=2;i=50007 (Boolean)</summary>
    public bool IsRobotWorking { get; set; }

    /// <summary>로봇 작업 완료 - ns=2;i=50008 (Boolean)</summary>
    public bool IsRobotDone { get; set; }

    #endregion

    #region 메타 데이터

    /// <summary>데이터 갱신 시간 (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>OPC UA Quality 상태 (Good=true)</summary>
    public bool IsGoodQuality { get; set; } = true;

    #endregion

    #region 메서드

    /// <summary>객체 복제</summary>
    public STMYoloData Clone() => new()
    {
        DeviceId = DeviceId,
        ChannelName = ChannelName,
        DeviceName = DeviceName,
        // Target (Write)
        TargetState = TargetState,
        TargetSpeedMain = TargetSpeedMain,
        TargetSpeedSort = TargetSpeedSort,
        TargetSpeedLoad = TargetSpeedLoad,
        AgvSortArrived = AgvSortArrived,
        AgvSortDeparted = AgvSortDeparted,
        AgvLoadArrived = AgvLoadArrived,
        AgvLoadDeparted = AgvLoadDeparted,
        // Current (Read)
        CurrentState = CurrentState,
        CurrentSpeedMain = CurrentSpeedMain,
        CurrentSpeedSort = CurrentSpeedSort,
        CurrentSpeedLoad = CurrentSpeedLoad,
        CurrentFloor = CurrentFloor,
        IsLiftMoving = IsLiftMoving,
        IsRobotWorking = IsRobotWorking,
        IsRobotDone = IsRobotDone,
        // Meta
        Timestamp = Timestamp,
        IsGoodQuality = IsGoodQuality
    };

    public override string ToString() =>
        $"[{DeviceId}] State={CurrentState} Floor={CurrentFloor} " +
        $"Speed(M:{CurrentSpeedMain},S:{CurrentSpeedSort},L:{CurrentSpeedLoad}) " +
        $"Lift={IsLiftMoving} Robot={IsRobotWorking}/{IsRobotDone}";

    #endregion
}

/// <summary>
/// STM_yolo 태그 이름 정의
/// </summary>
public class STMYoloTagNames
{
    // Write Tags (Target)
    public string TargetState { get; set; } = "TargetState";
    public string TargetSpeedMain { get; set; } = "TargetSpeedMain";
    public string TargetSpeedSort { get; set; } = "TargetSpeedSort";
    public string TargetSpeedLoad { get; set; } = "TargetSpeedLoad";
    public string AgvSortArrived { get; set; } = "AgvSortArrived";
    public string AgvSortDeparted { get; set; } = "AgvSortDeparted";
    public string AgvLoadArrived { get; set; } = "AgvLoadArrived";
    public string AgvLoadDeparted { get; set; } = "AgvLoadDeparted";

    // Read Tags (Current)
    public string CurrentState { get; set; } = "CurrentState";
    public string CurrentSpeedMain { get; set; } = "CurrentSpeedMain";
    public string CurrentSpeedSort { get; set; } = "CurrentSpeedSort";
    public string CurrentSpeedLoad { get; set; } = "CurrentSpeedLoad";
    public string CurrentFloor { get; set; } = "CurrentFloor";
    public string IsLiftMoving { get; set; } = "IsLiftMoving";
    public string IsRobotWorking { get; set; } = "IsRobotWorking";
    public string IsRobotDone { get; set; } = "IsRobotDone";
}

/// <summary>
/// STM_yolo 디바이스 태그 설정
/// KEPServerEX Item ID 형식: ChannelName.DeviceName.TagName
/// </summary>
public class STMYoloTagConfig
{
    /// <summary>디바이스 식별자</summary>
    public string DeviceId { get; set; } = "STM_yolo";

    /// <summary>KEPServerEX 채널명</summary>
    public string ChannelName { get; set; } = "STM";

    /// <summary>KEPServerEX 디바이스명</summary>
    public string DeviceName { get; set; } = "Stm_yolo";

    /// <summary>태그 이름 설정</summary>
    public STMYoloTagNames Tags { get; set; } = new();

    /// <summary>OPC UA 네임스페이스 인덱스</summary>
    public int NamespaceIndex { get; set; } = 2;

    /// <summary>
    /// 태그명으로 NodeId 생성
    /// KEPServerEX 형식: ns=2;s=ChannelName.DeviceName.TagName
    /// 예: ns=2;s=STM.Stm_yolo.TargetState
    /// </summary>
    public string GetNodeId(string tagName)
    {
        return $"ns={NamespaceIndex};s={ChannelName}.{DeviceName}.{tagName}";
    }

    /// <summary>
    /// 태그별 NodeId 매핑 (문자열 기반)
    /// </summary>
    public static class TagNames
    {
        // Write Tags (Target)
        public const string TargetState = "TargetState";
        public const string TargetSpeedMain = "TargetSpeedMain";
        public const string TargetSpeedSort = "TargetSpeedSort";
        public const string TargetSpeedLoad = "TargetSpeedLoad";
        public const string AgvSortArrived = "AgvSortArrived";
        public const string AgvSortDeparted = "AgvSortDeparted";
        public const string AgvLoadArrived = "AgvLoadArrived";
        public const string AgvLoadDeparted = "AgvLoadDeparted";

        // Read Tags (Current)
        public const string CurrentState = "CurrentState";
        public const string CurrentSpeedMain = "CurrentSpeedMain";
        public const string CurrentSpeedSort = "CurrentSpeedSort";
        public const string CurrentSpeedLoad = "CurrentSpeedLoad";
        public const string CurrentFloor = "CurrentFloor";
        public const string IsLiftMoving = "IsLiftMoving";
        public const string IsRobotWorking = "IsRobotWorking";
        public const string IsRobotDone = "IsRobotDone";
    }
}
