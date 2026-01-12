using System;
using System.Threading.Tasks;
using DeviceConnector.Models;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// ROS_ControlHub 서버와 통신하기 위한 클라이언트 인터페이스
    /// gRPC와 SignalR을 통해 ROS_ControlHub와 양방향 통신 수행
    /// </summary>
    public interface IRosControlHubClient : IDisposable
    {
        #region 속성

        /// <summary>gRPC 연결 상태</summary>
        bool IsGrpcConnected { get; }

        /// <summary>SignalR 연결 상태</summary>
        bool IsSignalRConnected { get; }

        /// <summary>ROS_ControlHub 서버 URL</summary>
        string ServerUrl { get; }

        #endregion

        #region 연결 관리

        /// <summary>
        /// ROS_ControlHub 서버에 연결 (gRPC + SignalR)
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 연결 해제
        /// </summary>
        Task DisconnectAsync();

        #endregion

        #region gRPC 명령 (ROS_ControlHub로 전송)

        /// <summary>
        /// 디바이스 상태 설정 명령 전송
        /// </summary>
        /// <param name="deviceId">디바이스 ID (예: "ESP32_01")</param>
        /// <param name="command">명령 (예: "start", "stop")</param>
        /// <param name="payloadJson">추가 파라미터 JSON</param>
        Task<DeviceCommandResult> SetDeviceStateAsync(string deviceId, string command, string payloadJson = "{}");

        /// <summary>
        /// 전체 디바이스 상태 설정 명령 전송
        /// </summary>
        /// <param name="command">명령 (예: "start_all", "stop_all")</param>
        Task<GlobalCommandResult> SetAllDevicesStateAsync(string command);

        /// <summary>
        /// AGV 이동 명령 전송
        /// </summary>
        Task<DeviceCommandResult> MoveAgvAsync(string deviceId, double x, double y);

        /// <summary>
        /// AGV 포인트 이동 명령 전송
        /// </summary>
        Task<DeviceCommandResult> MoveAgvToPointAsync(string deviceId, string pointName);

        #endregion

        #region SignalR 실시간 상태 수신

        /// <summary>
        /// 상태 업데이트 그룹에 참가
        /// </summary>
        Task JoinStateGroupAsync(string groupName = "default");

        /// <summary>
        /// 상태 업데이트 그룹에서 나가기
        /// </summary>
        Task LeaveStateGroupAsync(string groupName = "default");

        #endregion

        #region 이벤트

        /// <summary>ROS_ControlHub에서 시스템 상태 업데이트 수신 시</summary>
        event EventHandler<SystemStateEventArgs>? SystemStateUpdated;

        /// <summary>연결 상태 변경 시</summary>
        event EventHandler<HubConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>에러 발생 시</summary>
        event EventHandler<HubErrorEventArgs>? ErrorOccurred;

        #endregion
    }

    #region Result Models

    /// <summary>
    /// 디바이스 명령 결과
    /// </summary>
    public class DeviceCommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 전체 명령 결과
    /// </summary>
    public class GlobalCommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AffectedCount { get; set; }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// ROS_ControlHub 시스템 상태 이벤트
    /// </summary>
    public class SystemStateEventArgs : EventArgs
    {
        public DateTimeOffset Timestamp { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceStatus { get; set; } = string.Empty;
        public Dictionary<string, object> Extensions { get; set; } = new();

        /// <summary>OPC 연결 상태 (extensions에서 추출)</summary>
        public bool OpcConnected => Extensions.TryGetValue("opc.connected", out var val) && Convert.ToBoolean(val);

        /// <summary>컨베이어 동작 상태 (extensions에서 추출)</summary>
        public bool ConveyorRunning => Extensions.TryGetValue("opc.conveyor.running", out var val) && Convert.ToBoolean(val);

        /// <summary>컨베이어 속도 (extensions에서 추출)</summary>
        public double ConveyorSpeed => Extensions.TryGetValue("opc.conveyor.speed", out var val) ? Convert.ToDouble(val) : 0.0;
    }

    /// <summary>
    /// Hub 연결 상태 변경 이벤트
    /// </summary>
    public class HubConnectionChangedEventArgs : EventArgs
    {
        public bool IsGrpcConnected { get; set; }
        public bool IsSignalRConnected { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Hub 에러 이벤트
    /// </summary>
    public class HubErrorEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public string Source { get; set; } = string.Empty; // "gRPC" or "SignalR"
    }

    #endregion
}
