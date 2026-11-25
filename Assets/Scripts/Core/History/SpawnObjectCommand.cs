using System.Collections.Generic;
using UnityEngine;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Команда создания игрового объекта (одиночный объект, проп или участок дороги) с поддержкой отмены и повтора.
    /// При отмене объект деактивируется (SetActive(false)), при повторе — восстанавливается.
    /// </summary>
    public class SpawnObjectCommand : IReversibleCommand
    {
        private readonly GameObject _targetObject;
        private readonly string _description;
        private readonly List<(SnapPoint point, SnapPoint target)> _connections;

        public string Description => _description;
        public GameObject TargetObject => _targetObject;

        public SpawnObjectCommand(GameObject targetObject, string itemName = "Объект")
        {
            _targetObject = targetObject;
            _description = $"Создание: {itemName}";
            if (targetObject == null) return;

            var recorded = new HashSet<SnapPoint>();
            foreach (SnapPoint point in targetObject.GetComponentsInChildren<SnapPoint>())
            {
                SnapPoint target = point.ConnectedTo;
                if (target == null || recorded.Contains(point)) continue;

                _connections ??= new List<(SnapPoint point, SnapPoint target)>();
                _connections.Add((point, target));
                recorded.Add(target);
            }
        }

        public bool Execute()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning($"[SpawnObjectCommand] Невозможно повторить команду '{_description}': целевой объект был уничтожен.");
                return false;
            }

            if (_targetObject.activeSelf) return true;

            _targetObject.SetActive(true);
            if (_connections == null) return true;

            foreach (var connection in _connections)
            {
                if (connection.point != null && connection.point.Connect(connection.target))
                    continue;

                // Disabling the root also releases links restored earlier in this attempt.
                _targetObject.SetActive(false);
                return false;
            }

            return true;
        }

        public bool Undo()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning($"[SpawnObjectCommand] Невозможно отменить команду '{_description}': целевой объект был уничтожен.");
                return false;
            }

            _targetObject.SetActive(false);
            return true;
        }
    }
}
