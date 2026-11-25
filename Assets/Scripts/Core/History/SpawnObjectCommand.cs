using System;
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

        public string Description => _description;
        public GameObject TargetObject => _targetObject;

        public SpawnObjectCommand(GameObject targetObject, string itemName = "Объект")
        {
            _targetObject = targetObject;
            _description = $"Создание: {itemName}";
        }

        public bool Execute()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning($"[SpawnObjectCommand] Невозможно повторить команду '{_description}': целевой объект был уничтожен.");
                return false;
            }

            _targetObject.SetActive(true);
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
