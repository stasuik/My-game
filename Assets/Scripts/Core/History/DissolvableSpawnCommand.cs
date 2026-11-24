using System;
using UnityEngine;
using RoadBuilder.Core.FX;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Расширенная команда создания игрового объекта (дороги или декорации) с поддержкой плавного растворения
    /// </summary>
    public class DissolvableSpawnCommand : IReversibleCommand
    {
        private readonly GameObject _targetObject;
        private readonly string _description;
        private readonly VRDissolveEffect _dissolveEffect;

        public string Description => _description;
        public GameObject TargetObject => _targetObject;

        public DissolvableSpawnCommand(GameObject targetObject, string itemName = "Объект", bool autoAttachDissolve = true)
        {
            _targetObject = targetObject;
            _description = $"Создание: {itemName}";

            if (_targetObject != null && autoAttachDissolve)
            {
                _dissolveEffect = _targetObject.GetComponent<VRDissolveEffect>();
                if (_dissolveEffect == null)
                {
                    _dissolveEffect = _targetObject.AddComponent<VRDissolveEffect>();
                }
            }
        }

        public bool Execute()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning($"[DissolvableSpawnCommand] Объект '{_description}' был уничтожен.");
                return false;
            }

            if (_dissolveEffect != null && Application.isPlaying)
            {
                _dissolveEffect.PlayDissolveIn();
            }
            else
            {
                _targetObject.SetActive(true);
            }
            return true;
        }

        public bool Undo()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning($"[DissolvableSpawnCommand] Объект '{_description}' был уничтожен.");
                return false;
            }

            if (_dissolveEffect != null && Application.isPlaying)
            {
                _dissolveEffect.PlayDissolveOut();
            }
            else
            {
                _targetObject.SetActive(false);
            }
            return true;
        }
    }
}
