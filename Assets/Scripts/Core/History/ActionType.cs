using System;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Типы обратимых действий пользователя в редакторе дорог
    /// </summary>
    public enum ActionType : byte
    {
        CreateRoad = 0,
        DeleteRoad = 1,
        TransformRoad = 2,
        ModifyRoadProperties = 3,
        ConnectSnapPoints = 4,
        DisconnectSnapPoints = 5
    }
}
