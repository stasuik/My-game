using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRConstructionController : MonoBehaviour
{
    private enum ConstructionState { Idle, Building }

    [Header("Ссылки на компоненты")]
    public VRRoadBuilder roadBuilder;
    public BuildManager buildManager;
    public Transform handTransform;
    public InputActionReference triggerAction; // Сюда привязан ваш курок

    [Header("Настройки луча")]
    public float maxBuildDistance = 50f;

    private ConstructionState currentState = ConstructionState.Idle;
    private List<Vector3> roadPoints = new List<Vector3>();

    private bool wasTriggerPressedLastFrame = false;

    private void Update()
    {
        if (buildManager == null || roadBuilder == null || triggerAction == null || triggerAction.action != null)
        {
            // ПРОВЕРКА НАПРЯМУЮ: если action не инициализирован — выходим, чтобы не было ошибок
            if (triggerAction?.action == null) return;
        }

        // Надежный опрос курка в реальном времени
        bool isTriggerHeld = triggerAction.action.IsPressed() || triggerAction.action.ReadValue<float>() > 0.5f;

        var menuController = GetComponent<VRMenuController>();
        if (menuController != null && menuController.menuCanvas != null && menuController.menuCanvas.activeSelf)
        {
            if (currentState == ConstructionState.Building) CancelConstruction();
            return;
        }

        // РЕЖИМ 1: НАЖАТИЕ КУРКА
        if (isTriggerHeld && !wasTriggerPressedLastFrame)
        {
            if (buildManager.currentMode == BuildManager.BuildMode.Road)
            {
                currentState = ConstructionState.Building;
                roadPoints.Clear();

                if (GetLaserHitPoint(out Vector3 hitPoint, out Vector3 normal))
                {
                    roadPoints.Add(hitPoint);
                }
            }
            else
            {
                HandleSinglePlacement();
            }
        }

        // РЕЖИМ 2: УДЕРЖАНИЕ КУРКА (ТЯНЕМ ДОРОГУ)
        if (isTriggerHeld && wasTriggerPressedLastFrame && currentState == ConstructionState.Building)
        {
            if (buildManager.currentMode == BuildManager.BuildMode.Road && GetLaserHitPoint(out Vector3 currentHitPoint, out Vector3 normal))
            {
                Vector3 lastFixedPoint = roadPoints[roadPoints.Count - 1];

                float distanceXZ = Vector2.Distance(new Vector2(lastFixedPoint.x, lastFixedPoint.z),
                                                    new Vector2(currentHitPoint.x, currentHitPoint.z));

                // Если рука ушла дальше 146 метров — фиксируем монолитную точку в память
                if (distanceXZ >= roadBuilder.stepDistance)
                {
                    Vector3 direction = (currentHitPoint - lastFixedPoint).normalized;
                    Vector3 nextPoint = lastFixedPoint + (direction * roadBuilder.stepDistance);

                    nextPoint = GetTerrainHeightAt(nextPoint);
                    roadPoints.Add(nextPoint);
                    lastFixedPoint = nextPoint; // Обновляем локальную переменную для расчета хвоста
                }

                // ИСПРАВЛЕНО: Создаем массив для превью
                List<Vector3> previewPoints = new List<Vector3>(roadPoints);

                // Магия плавности: если джойстик отошел от последней фиксированной точки хотя бы на 20 см,
                // мы принудительно добавляем текущую точку луча в качестве финала. 
                // Это гарантирует, что points.Count ВСЕГДА будет >= 2, и призрак отобразится сразу!
                if (Vector3.Distance(lastFixedPoint, currentHitPoint) > 0.2f)
                {
                    previewPoints.Add(currentHitPoint);
                }
                else if (previewPoints.Count == 1)
                {
                    // Защита для самого первого кадра: дублируем стартовую точку, чтобы не ломать метод
                    previewPoints.Add(currentHitPoint + new Vector3(0.01f, 0, 0));
                }

                // Непрерывно рисуем призрак дороги
                roadBuilder.DrawGhostRoad(previewPoints);
            }
        }

        // РЕЖИМ 3: ОТПУСКАНИЕ КУРКА
        if (!isTriggerHeld && wasTriggerPressedLastFrame)
        {
            if (currentState == ConstructionState.Building)
            {
                currentState = ConstructionState.Idle;

                // Запекаем готовую дорогу
                roadBuilder.BuildFinalRoad(roadPoints);
                roadPoints.Clear();
            }
        }

        wasTriggerPressedLastFrame = isTriggerHeld;
    }


    private void CancelConstruction()
    {
        currentState = ConstructionState.Idle;
        roadPoints.Clear();
        if (roadBuilder != null) roadBuilder.ClearGhostRoad();
    }

    private bool GetLaserHitPoint(out Vector3 hitPoint, out Vector3 hitNormal)
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxBuildDistance, roadBuilder.terrainLayer))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            return true;
        }
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        return false;
    }

    private Vector3 GetTerrainHeightAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 100f, roadBuilder.terrainLayer))
        {
            return hit.point;
        }
        return originPos;
    }

    private void HandleSinglePlacement()
    {
        if (GetLaserHitPoint(out Vector3 spawnPoint, out Vector3 groundNormal))
        {
            GameObject prefabToSpawn = null;

            if (buildManager.currentMode == BuildManager.BuildMode.BusStop) prefabToSpawn = buildManager.busStopPrefab;
            if (buildManager.currentMode == BuildManager.BuildMode.Border) prefabToSpawn = buildManager.borderPrefab;

            if (prefabToSpawn == null) return;

            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            Vector3 lookDirection = handTransform.position - spawnPoint;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                Quaternion facePlayerRot = Quaternion.LookRotation(lookDirection);
                spawnRotation = facePlayerRot * Quaternion.FromToRotation(Vector3.up, groundNormal);
            }

            Instantiate(prefabToSpawn, spawnPoint, spawnRotation);
            roadBuilder.ClearGhostRoad();
        }
    }

    // На всякий случай очищаем призраков при выключении скрипта
    private void OnDisable()
    {
        CancelConstruction();
        wasTriggerPressedLastFrame = false;
    }
}
