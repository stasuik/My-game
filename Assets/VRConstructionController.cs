using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRConstructionController : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public VRRoadBuilder roadBuilder;        // Ссылка на наш менеджер дорог
    public Transform handTransform;          // Точка, откуда летит луч (обычно объект контроллера)
    public InputActionReference triggerAction; // Кнопка курка контроллера через Input System

    [Header("Настройки луча")]
    public float maxBuildDistance = 50f;     // Максимальная дальность строительства

    private bool isBuilding = false;

    private void OnEnable()
    {
        // Подписываемся на событие нажатия курка
        if (triggerAction != null && triggerAction.action != null)
        {
            triggerAction.action.started += OnTriggerPressed;
        }
    }

    private void OnDisable()
    {
        // Отписываемся от события при выключении скрипта
        if (triggerAction != null && triggerAction.action != null)
        {
            triggerAction.action.started -= OnTriggerPressed;
        }
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        // Пускаем физический луч из руки вперед
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        RaycastHit hit;

        // Ищем столкновение только со слоем земли (terrainLayer настроен в VRRoadBuilder)
        if (Physics.Raycast(ray, out hit, maxBuildDistance, roadBuilder.terrainLayer))
        {
            Vector3 spawnPoint = hit.point;

            if (!isBuilding)
            {
                // Если мы еще не строили — начинаем новую дорогу
                roadBuilder.StartNewRoad(spawnPoint);
                isBuilding = true;
            }
            else
            {
                // Если дорога уже строится — добавляем следующую точку
                roadBuilder.AddPointToRoad(spawnPoint);
            }
        }
    }

    // Метод для завершения текущей дороги (например, можно будет вызвать другой кнопкой)
    public void ResetConstruction()
    {
        isBuilding = false;
        roadBuilder.FinishRoad();
    }
}

