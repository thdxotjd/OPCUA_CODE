using System;
using System.Threading.Tasks;
using DeviceConnector.Events;
using DeviceConnector.Models;

namespace DeviceConnector.Interfaces
{
    /// <summary>
    /// RoboDK OPC UA 클라이언트 서비스 인터페이스
    /// OPC UA Method 호출을 통해 로봇 Joint 값 읽기/쓰기
    /// </summary>
    public interface IRoboDkOpcUaService : IDisposable
    {
        #region 속성

        /// <summary>연결 상태 정보</summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>연결 여부</summary>
        bool IsConnected { get; }

        /// <summary>마지막으로 읽은 Joint 데이터</summary>
        RobotJointData? LastJointData { get; }

        #endregion

        #region 연결 관리

        /// <summary>
        /// RoboDK OPC UA 서버에 연결
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 연결 해제
        /// </summary>
        Task DisconnectAsync();

        #endregion

        #region Robot Joint 읽기/쓰기

        /// <summary>
        /// 로봇 Joint 값을 문자열로 읽기 (getJointsStr Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름 (예: "ABB CRB 1300-7/1.4")</param>
        /// <returns>Joint 값 문자열 (예: "0,0,0,0,0,0")</returns>
        Task<string?> GetJointsStrAsync(string robotName);

        /// <summary>
        /// 로봇 Joint 값을 배열로 읽기 (getJoints Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <returns>Joint 값 배열 (double[])</returns>
        Task<double[]?> GetJointsAsync(string robotName);

        /// <summary>
        /// 로봇 Joint 값을 문자열로 설정 (setJointsStr Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <param name="jointsStr">Joint 값 문자열 (예: "0,0,0,0,0,0")</param>
        Task<bool> SetJointsStrAsync(string robotName, string jointsStr);

        /// <summary>
        /// 로봇 Joint 값을 배열로 설정 (setJoints Method 호출)
        /// </summary>
        /// <param name="robotName">로봇 이름</param>
        /// <param name="joints">Joint 값 배열</param>
        Task<bool> SetJointsAsync(string robotName, double[] joints);

        /// <summary>
        /// RoboDK 아이템 정보 가져오기
        /// </summary>
        Task<string?> GetItemAsync(string itemName);

        #endregion

        #region Variable 읽기 (보조 기능)

        /// <summary>
        /// 시뮬레이션 속도 읽기
        /// </summary>
        Task<double?> GetSimulationSpeedAsync();

        /// <summary>
        /// Station 이름 읽기
        /// </summary>
        Task<string?> GetStationNameAsync();

        /// <summary>
        /// RoboDK 버전 정보 읽기
        /// </summary>
        Task<string?> GetRoboDkVersionAsync();

        #endregion

        #region 이벤트

        /// <summary>Joint 데이터 변경 시 발생</summary>
        event EventHandler<RobotJointChangedEventArgs>? JointDataChanged;

        /// <summary>연결 상태 변경 시 발생</summary>
        event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>에러 발생 시</summary>
        event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        #endregion
    }
}
