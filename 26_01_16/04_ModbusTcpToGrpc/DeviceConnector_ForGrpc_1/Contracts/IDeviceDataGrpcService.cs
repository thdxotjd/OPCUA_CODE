using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceConnector.Contracts
{
    /// <summary>
    /// gRPC 서비스 인터페이스
    /// gRPC 개발자가 이 인터페이스를 구현하여 DeviceConnector와 통신
    /// 
    /// Proto 파일 생성 시 참조:
    /// - service DeviceDataService { ... }
    /// </summary>
    public interface IDeviceDataGrpcService
    {
        #region 단일 읽기

        /// <summary>
        /// 디바이스 전체 데이터 읽기
        /// rpc ReadData (ReadDataRequest) returns (ReadDataResponse);
        /// </summary>
        Task<ReadDataResponse> ReadDataAsync(ReadDataRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 개별 태그 읽기
        /// rpc ReadTag (ReadTagRequest) returns (ReadTagResponse);
        /// </summary>
        Task<ReadTagResponse> ReadTagAsync(ReadTagRequest request, CancellationToken cancellationToken = default);

        #endregion

        #region 쓰기

        /// <summary>
        /// 태그 값 쓰기
        /// rpc WriteTag (WriteTagRequest) returns (WriteTagResponse);
        /// </summary>
        Task<WriteTagResponse> WriteTagAsync(WriteTagRequest request, CancellationToken cancellationToken = default);

        #endregion

        #region 스트리밍 (Server Streaming)

        /// <summary>
        /// 실시간 데이터 구독 (Server Streaming)
        /// rpc SubscribeData (SubscribeRequest) returns (stream SubscribeDataResponse);
        /// </summary>
        IAsyncEnumerable<SubscribeDataResponse> SubscribeDataAsync(SubscribeRequest request, CancellationToken cancellationToken = default);

        #endregion

        #region 연결 관리

        /// <summary>
        /// 연결 상태 조회
        /// rpc GetConnectionStatus (Empty) returns (ConnectionStatusResponse);
        /// </summary>
        Task<ConnectionStatusResponse> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

        #endregion
    }

    /// <summary>
    /// gRPC 클라이언트가 DeviceConnector 데이터를 가져올 때 사용하는 Provider 인터페이스
    /// DeviceConnector 측에서 구현하여 gRPC 서비스에 주입
    /// </summary>
    public interface IDeviceDataProvider
    {
        /// <summary>
        /// 디바이스 데이터 읽기
        /// </summary>
        Task<DeviceDataDto?> GetDeviceDataAsync(string deviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 태그 데이터 읽기
        /// </summary>
        Task<TagDataDto?> GetTagDataAsync(string deviceId, string tagName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 태그 쓰기
        /// </summary>
        Task<bool> WriteTagAsync(string deviceId, string tagName, object value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 연결 상태 조회
        /// </summary>
        ConnectionStatusResponse GetConnectionStatus();

        /// <summary>
        /// 데이터 변경 이벤트
        /// </summary>
        event EventHandler<DeviceDataChangedEventArgs>? DataChanged;
    }

    /// <summary>
    /// 데이터 변경 이벤트 인자
    /// </summary>
    public class DeviceDataChangedEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public DeviceDataDto Data { get; }
        public DateTime Timestamp { get; }

        public DeviceDataChangedEventArgs(string deviceId, DeviceDataDto data)
        {
            DeviceId = deviceId;
            Data = data;
            Timestamp = DateTime.UtcNow;
        }
    }
}
