using System;
using UnityEngine;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Структура снимка состояния дорожного сегмента для отмены и повтора
    /// </summary>
    [Serializable]
    public struct RoadActionSnapshot
    {
        public string ObjectId;
        public string PrefabId;
        public ActionType ActionType;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public int LaneCount;
        public int RoadType;
        public float Length;
        public string[] ConnectedNeighbors;
        public long Timestamp;

        public RoadActionSnapshot(
            string objectId,
            string prefabId,
            ActionType actionType,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            int laneCount = 2,
            int roadType = 0,
            float length = 10f,
            string[] connectedNeighbors = null)
        {
            ObjectId = objectId;
            PrefabId = prefabId;
            ActionType = actionType;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            LaneCount = laneCount;
            RoadType = roadType;
            Length = length;
            ConnectedNeighbors = connectedNeighbors ?? Array.Empty<string>();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
