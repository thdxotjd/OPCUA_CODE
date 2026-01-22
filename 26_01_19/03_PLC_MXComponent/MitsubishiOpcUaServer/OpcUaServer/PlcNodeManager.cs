using System;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Server;
using MitsubishiOpcUaServer.Models;
using Microsoft.Extensions.Logging;

namespace MitsubishiOpcUaServer.OpcUaServer
{
    /// <summary>
    /// OPC UA 노드 매니저 - PLC 태그를 OPC UA 노드로 관리
    /// </summary>
    public class PlcNodeManager : CustomNodeManager2
    {
        private readonly ILogger _logger;
        private readonly DeviceConfig _config;
        private readonly Dictionary<string, BaseDataVariableState> _tagNodes;
        private FolderState? _deviceFolder;

        public PlcNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            DeviceConfig config,
            ILogger logger)
            : base(server, configuration, "http://mitsubishi.opcua.server")
        {
            _config = config;
            _logger = logger;
            _tagNodes = new Dictionary<string, BaseDataVariableState>();

            SystemContext.NodeIdFactory = this;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                base.CreateAddressSpace(externalReferences);

                // 루트 폴더 생성
                var rootFolder = CreateFolder(null, "PLC", "PLC");
                rootFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                AddExternalReference(ObjectIds.ObjectsFolder, ReferenceTypes.Organizes, false, rootFolder.NodeId, externalReferences);

                // 채널 폴더 생성
                var channelFolder = CreateFolder(rootFolder, _config.ChannelName, _config.ChannelName);

                // 디바이스 폴더 생성
                _deviceFolder = CreateFolder(channelFolder, _config.DeviceName, _config.DeviceName);

                // 연결 상태 노드
                CreateVariable(_deviceFolder, "IsConnected", "IsConnected", DataTypeIds.Boolean, false);

                // 타임스탬프 노드
                CreateVariable(_deviceFolder, "Timestamp", "Timestamp", DataTypeIds.DateTime, DateTime.UtcNow);

                // 태그 노드 생성
                foreach (var tag in _config.Tags)
                {
                    string tagAlias = tag.Key;
                    string plcAddress = tag.Value;

                    // 주소에 따라 데이터 타입 결정
                    NodeId dataType = GetDataTypeFromAddress(plcAddress);
                    object defaultValue = GetDefaultValueFromAddress(plcAddress);

                    var variable = CreateVariable(_deviceFolder, tagAlias, tagAlias, dataType, defaultValue);
                    variable.Description = new LocalizedText($"PLC Address: {plcAddress}");

                    _tagNodes[tagAlias] = variable;
                    _logger.LogInformation("OPC UA 노드 생성: {TagAlias} -> {PlcAddress}", tagAlias, plcAddress);
                }

                AddPredefinedNode(SystemContext, rootFolder);
                _logger.LogInformation("OPC UA Address Space 생성 완료. 태그 수: {Count}", _tagNodes.Count);
            }
        }

        /// <summary>
        /// DeviceData로 모든 노드 업데이트
        /// </summary>
        public void UpdateNodes(DeviceData deviceData)
        {
            lock (Lock)
            {
                try
                {
                    // 연결 상태 업데이트
                    if (_tagNodes.TryGetValue("IsConnected", out var connNode))
                    {
                        connNode.Value = deviceData.IsConnected;
                        connNode.Timestamp = DateTime.UtcNow;
                        connNode.ClearChangeMasks(SystemContext, false);
                    }

                    // 타임스탬프 업데이트
                    if (_tagNodes.TryGetValue("Timestamp", out var tsNode))
                    {
                        tsNode.Value = deviceData.Timestamp;
                        tsNode.Timestamp = DateTime.UtcNow;
                        tsNode.ClearChangeMasks(SystemContext, false);
                    }

                    // 태그 값 업데이트
                    foreach (var tag in deviceData.Tags)
                    {
                        if (_tagNodes.TryGetValue(tag.Key, out var node))
                        {
                            node.Value = tag.Value;
                            node.Timestamp = DateTime.UtcNow;
                            node.StatusCode = StatusCodes.Good;
                            node.ClearChangeMasks(SystemContext, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OPC UA 노드 업데이트 중 오류");
                }
            }
        }

        /// <summary>
        /// 태그 값 쓰기 (OPC UA 클라이언트에서 호출)
        /// </summary>
        public bool WriteTag(string tagAlias, object value)
        {
            lock (Lock)
            {
                if (_tagNodes.TryGetValue(tagAlias, out var node))
                {
                    node.Value = value;
                    node.Timestamp = DateTime.UtcNow;
                    node.ClearChangeMasks(SystemContext, false);
                    return true;
                }
                return false;
            }
        }

        #region Helper Methods

        private FolderState CreateFolder(NodeState? parent, string name, string displayName)
        {
            var folder = new FolderState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(name, NamespaceIndex),
                BrowseName = new QualifiedName(name, NamespaceIndex),
                DisplayName = new LocalizedText(displayName),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            parent?.AddChild(folder);
            return folder;
        }

        private BaseDataVariableState CreateVariable(
            NodeState parent,
            string name,
            string displayName,
            NodeId dataType,
            object defaultValue)
        {
            var variable = new BaseDataVariableState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId($"{_config.ChannelName}.{_config.DeviceName}.{name}", NamespaceIndex),
                BrowseName = new QualifiedName(name, NamespaceIndex),
                DisplayName = new LocalizedText(displayName),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                DataType = dataType,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Historizing = false,
                Value = defaultValue,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };

            parent?.AddChild(variable);
            _tagNodes[name] = variable;
            return variable;
        }

        private NodeId GetDataTypeFromAddress(string address)
        {
            string deviceType = address.ToUpper();
            
            // 비트 디바이스
            if (deviceType.StartsWith("M") || deviceType.StartsWith("X") || 
                deviceType.StartsWith("Y") || deviceType.StartsWith("B") ||
                deviceType.StartsWith("L") || deviceType.StartsWith("F"))
            {
                return DataTypeIds.Boolean;
            }

            // 워드 디바이스 (기본 Int16)
            return DataTypeIds.Int16;
        }

        private object GetDefaultValueFromAddress(string address)
        {
            string deviceType = address.ToUpper();
            
            // 비트 디바이스
            if (deviceType.StartsWith("M") || deviceType.StartsWith("X") || 
                deviceType.StartsWith("Y") || deviceType.StartsWith("B") ||
                deviceType.StartsWith("L") || deviceType.StartsWith("F"))
            {
                return false;
            }

            // 워드 디바이스
            return (short)0;
        }

        #endregion
    }
}
