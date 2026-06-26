using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRConstructionController : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public VRRoadBuilder roadBuilder;        // Менеджер дорог
    public BuildManager buildManager;        // Менеджер режимов (тот самый, что мы создали)
    public Transform handTransform;          // Откуда летит луч (контроллер)
    public InputActionReference triggerAction; // Кнопка курка

    [Header("Настройки луча")]
    public float maxBuildDistance = 50f;     // Дальность луча

    private bool isBuildingRoad = false;

    private void OnEnable()
    {
        if (triggerAction != null && triggerAction.action != null)
        {
            triggerAction.action.started += OnTriggerPressed;
        }
    }

    private void OnDisable()
    {
        if (triggerAction != null && triggerAction.action != null)
        {
            triggerAction.action.started -= OnTriggerPressed;
        }
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        RaycastHit hit;

        // Пускаем луч в землю (слой земли берем из roadBuilder)
        if (Physics.Raycast(ray, out hit, maxBuildDistance, roadBuilder.terrainLayer))
        {
            Vector3 spawnPoint = hit.point;

            // Проверяем, какой режим сейчас выбран в меню
            switch (buildManager.currentMode)
            {
                case BuildManager.BuildMode.Road:
                    BuildRoadLogic(spawnPoint);
                    break;

                case BuildManager.BuildMode.BusStop:
                    SpawnObjectLogic(buildManager.busStopPrefab, spawnPoint, hit.normal);
                    break;

                case BuildManager.BuildMode.Border:
                    SpawnObjectLogic(buildManager.borderPrefab, spawnPoint, hit.normal);
                    break;
            }
        }
    }

    // Логика строительства дорог
    private void BuildRoadLogic(Vector3 point)
    {
        if (!isBuildingRoad)
        {
            roadBuilder.StartNewRoad(point);
            isBuildingRoad = true;
        }
        else
        {
            roadBuilder.AddPointToRoad(point);
        }
    }

    // Логика установки объектов (остановки, бордюры) с учетом неровности холма
    private void SpawnObjectLogic(GameObject prefab, Vector3 position, Vector3 groundNormal)
    {
        if (prefab == null)
        {
            Debug.LogError("Префаб объекта не назначен в BuildManager!");
            return;
        }

        // Вычисляем поворот объекта так, чтобы он стоял ровно на холме (hit.normal — это перпендикуляр к холму)
        Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);

        // Дополнительно разворачиваем объект лицом к игроку, чтобы он не спавнился задом наперед
        Vector3 lookDirection = handTransform.position - position;
        lookDirection.y = 0; // Игнорируем высоту взгляда, чтобы объект не наклонялся к руке
        if (lookDirection != Vector3.zero)
        {
            Quaternion facePlayerRot = Quaternion.LookRotation(lookDirection);
            spawnRotation = facePlayerRot * Quaternion.FromToRotation(Vector3.up, groundNormal);
        }

        // Создаем объект на сцене
        Instantiate(prefab, position, spawnRotation);

        // Завершаем режим строительства дороги, если мы переключились на объекты
        ResetRoadBuilding();
    }

    public void ResetRoadBuilding()
    {
        isBuildingRoad = false;
        roadBuilder.FinishRoad();
    }
}


