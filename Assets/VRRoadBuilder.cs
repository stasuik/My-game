using UnityEngine;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    [Header("Настройки строительства")]
    public GameObject roadSegmentPrefab;     // Сюда перетащите ваш красивый асфальтовый префаб (Road_1X_Straight)
    public GameObject ghostSegmentPrefab;    // Сюда перетащите ПРИЗРАКА (модель-стрелочку, кубик или дубликат дороги с прозрачным шейдером)
    public LayerMask terrainLayer;           // Слой ландшафта (Terrain)

    [Header("Параметры шага")]
    public float stepDistance = 146.304f;    // Точная длина вашей модели по оси X
    public float roadHeightOffset = 0.02f;   // Высота дороги над землей

    [Tooltip("Дополнительный доворот модели, если она встает не той стороной")]
    public Vector3 modelRotationOffset = Vector3.zero;

    private GameObject ghostContainer;
    private List<GameObject> activeGhostSegments = new List<GameObject>();

    /// <summary>
    /// Отрисовка временного "призрачного" пути (пока игрок держит курок)
    /// </summary>
    public void DrawGhostRoad(List<Vector3> points)
    {
        ClearGhostRoad();

        // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Если вы забыли назначить префаб призрака, скрипт безопасно использует обычную дорогу
        GameObject prefabToUse = ghostSegmentPrefab != null ? ghostSegmentPrefab : roadSegmentPrefab;
        if (points.Count < 2 || prefabToUse == null) return;

        ghostContainer = new GameObject("[GHOST_ROAD]");

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += roadHeightOffset;

            Vector3 groundNormal = GetTerrainNormalAt(spawnPos);

            // ФОРМУЛА СИНХРОНИЗИРОВАНА: Теперь базовый разворот в обоих методах абсолютно одинаковый (0, 0, 0)
            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(0, 0, 0);
            spawnRot *= Quaternion.Euler(modelRotationOffset);

            GameObject ghostSeg = Instantiate(prefabToUse, spawnPos, spawnRot, ghostContainer.transform);

            // Отключаем физику у призраков, чтобы луч лазера не спотыкался о них
            foreach (var col in ghostSeg.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Динамическое сжатие/растяжение последнего хвоста по оси X
            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / stepDistance;
                Vector3 currentScale = ghostSeg.transform.localScale;
                ghostSeg.transform.localScale = new Vector3(currentScale.x * scaleMultiplier, currentScale.y, currentScale.z);
            }

            activeGhostSegments.Add(ghostSeg);
        }
    }

    /// <summary>
    /// Строит финальную физическую дорогу на сцене (когда игрок отпустил курок)
    /// </summary>
    public void BuildFinalRoad(List<Vector3> points)
    {
        ClearGhostRoad();
        if (points.Count < 2 || roadSegmentPrefab == null) return;

        GameObject finalRoadContainer = new GameObject("Built_Road_Segment");

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += roadHeightOffset;

            Vector3 groundNormal = GetTerrainNormalAt(spawnPos);

            // ФОРМУЛА СИНХРОНИЗИРОВАНА: Точно такой же разворот, как и у призрака
            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(0, 0, 0);
            spawnRot *= Quaternion.Euler(modelRotationOffset);

            GameObject permanentSegment = Instantiate(roadSegmentPrefab, spawnPos, spawnRot, finalRoadContainer.transform);

            // Точное масштабирование крайнего куска по оси X до точки лазера
            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / stepDistance;
                Vector3 currentScale = permanentSegment.transform.localScale;
                permanentSegment.transform.localScale = new Vector3(currentScale.x * scaleMultiplier, currentScale.y, currentScale.z);
            }

            // Автоматически добавляем MeshCollider на все вложенные меши (дорогу и бордюры)
            foreach (var renderer in permanentSegment.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.GetComponent<Collider>() == null)
                {
                    renderer.gameObject.AddComponent<MeshCollider>();
                }
            }
        }
    }

    public void ClearGhostRoad()
    {
        if (ghostContainer != null) Destroy(ghostContainer);
        activeGhostSegments.Clear();
    }

    private Vector3 GetTerrainNormalAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 100f, terrainLayer))
        {
            return hit.normal;
        }
        return Vector3.up;
    }
}
