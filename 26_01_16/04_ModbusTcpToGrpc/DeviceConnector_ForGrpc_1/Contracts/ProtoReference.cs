// ============================================
// DeviceConnector gRPC Proto 정의 (참조용)
// gRPC 개발자가 이 파일을 기반으로 Proto 생성
// ============================================
//
// syntax = "proto3";
// option csharp_namespace = "DeviceConnector.Grpc";
// package deviceconnector;
//
// // ============================================
// // 서비스 정의
// // ============================================
// service DeviceDataService {
//   // 디바이스 전체 데이터 읽기
//   rpc ReadData (ReadDataRequest) returns (ReadDataResponse);
//   
//   // 개별 태그 읽기
//   rpc ReadTag (ReadTagRequest) returns (ReadTagResponse);
//   
//   // 태그 쓰기
//   rpc WriteTag (WriteTagRequest) returns (WriteTagResponse);
//   
//   // 실시간 데이터 구독 (Server Streaming)
//   rpc SubscribeData (SubscribeRequest) returns (stream SubscribeDataResponse);
//   
//   // 연결 상태 조회
//   rpc GetConnectionStatus (Empty) returns (ConnectionStatusResponse);
// }
//
// // ============================================
// // 메시지 정의
// // ============================================
// message Empty {}
//
// message DeviceDataDto {
//   string device_id = 1;
//   string channel_name = 2;
//   int32 pos_x = 3;           // Word (short)
//   int32 pos_y = 4;           // Word (short)
//   bool state = 5;            // Boolean
//   bool to = 6;               // Boolean
//   int64 timestamp_utc = 7;   // Unix timestamp (milliseconds)
//   bool is_good_quality = 8;
// }
//
// message TagDataDto {
//   string device_id = 1;
//   string tag_name = 2;
//   string node_id = 3;
//   oneof value {
//     int32 int_value = 4;
//     bool bool_value = 5;
//   }
//   int32 data_type = 6;       // 0=Word, 1=Boolean
//   int64 timestamp_utc = 7;
// }
//
// message ReadDataRequest {
//   string device_id = 1;
// }
//
// message ReadDataResponse {
//   bool success = 1;
//   string error_message = 2;
//   DeviceDataDto data = 3;
// }
//
// message ReadTagRequest {
//   string device_id = 1;
//   string tag_name = 2;
// }
//
// message ReadTagResponse {
//   bool success = 1;
//   string error_message = 2;
//   TagDataDto data = 3;
// }
//
// message WriteTagRequest {
//   string device_id = 1;
//   string tag_name = 2;
//   oneof value {
//     int32 int_value = 3;
//     bool bool_value = 4;
//   }
// }
//
// message WriteTagResponse {
//   bool success = 1;
//   string error_message = 2;
// }
//
// message SubscribeRequest {
//   string device_id = 1;
//   int32 sampling_interval_ms = 2;
// }
//
// message SubscribeDataResponse {
//   DeviceDataDto data = 1;
//   int64 sequence_number = 2;
// }
//
// message ConnectionStatusResponse {
//   bool is_connected = 1;
//   string state = 2;
//   string endpoint_url = 3;
//   int64 connected_since_utc = 4;
//   int32 reconnect_attempts = 5;
//   string last_error = 6;
// }

namespace DeviceConnector.Contracts
{
    /// <summary>
    /// gRPC 개발자 참조용 - KEPServerEX 태그 정보
    /// </summary>
    public static class KepwareTagInfo
    {
        /// <summary>
        /// 태그 정보
        /// </summary>
        public static class Tags
        {
            /// <summary>POS_X - Address: 40001, DataType: Word</summary>
            public const string POS_X = "POS_X";

            /// <summary>POS_Y - Address: 40002, DataType: Word</summary>
            public const string POS_Y = "POS_Y";

            /// <summary>State - Address: 40003.0, DataType: Boolean</summary>
            public const string STATE = "State";

            /// <summary>To - Address: 40004.0, DataType: Boolean</summary>
            public const string TO = "To";
        }

        /// <summary>
        /// OPC UA 노드 ID 형식
        /// 예: ns=2;s=ModbusTCP.ESP32_01.POS_X
        /// </summary>
        public static string GetNodeId(string channelName, string deviceName, string tagName)
        {
            return $"ns=2;s={channelName}.{deviceName}.{tagName}";
        }

        /// <summary>
        /// 기본 채널명
        /// </summary>
        public const string DefaultChannelName = "ModbusTCP";

        /// <summary>
        /// 기본 디바이스명
        /// </summary>
        public const string DefaultDeviceName = "ESP32_01";
    }
}
