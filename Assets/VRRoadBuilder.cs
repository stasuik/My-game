using UnityEngine;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    [Header("Настройки слоев")]
    [Tooltip("Слой ландшафта (Terrain), по которому лазер определяет землю")]
    public LayerMask terrainLayer;

    [Tooltip("Дополнительный доворот моделей в локальных координатах, если они встают боком")]
    public Vector3 modelRotationOffset = Vector3.zero;

    [Header("Настройки мостов")]
    [Tooltip("Префаб вертикальной колонны-опоры, которая будет расти вниз до земли")]
    public GameObject bridgePillarPrefab;

    private GameObject ghostContainer;

    /// <summary>
    /// Отрисовка временного "призрачного" пути (пока игрок держит курок)
    /// </summary>
    public void DrawGhostRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();

        GameObject prefabToUse = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (points.Count < 2 || prefabToUse == null) return;

        ghostContainer = new GameObject("[GHOST_STRUCTURE]");

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += item.heightOffset;

            // Если объект привязан к земле — берем нормаль земли, если это мост — смотрим строго вверх
            Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(spawnPos) : Vector3.up;

            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(modelRotationOffset);

            GameObject ghostSeg = Instantiate(prefabToUse, spawnPos, spawnRot, ghostContainer.transform);

            // Отключаем физику у призраков, чтобы луч лазера не спотыкался о них
            foreach (var col in ghostSeg.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Динамическое масштабирование последнего хвоста по оси Z до точки лазера
            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / item.stepDistance;
                Vector3 currentScale = ghostSeg.transform.localScale;
                ghostSeg.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);
            }

            // Спавн призрака опоры, если это мост
            if (!item.snapToTerrain && bridgePillarPrefab != null)
            {
                SpawnPillar(spawnPos, ghostContainer.transform, true);
            }
        }
    }

    /// <summary>
    /// Строит финальную физическую дорогу на сцене (когда игрок отпустил курок)
    /// </summary>
    public void BuildFinalRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2 || item.finalPrefab == null) return;

        GameObject finalContainer = new GameObject("Built_" + item.itemName);

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += item.heightOffset;

            Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(spawnPos) : Vector3.up;

            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(modelRotationOffset);

            GameObject permanentSegment = Instantiate(item.finalPrefab, spawnPos, spawnRot, finalContainer.transform);

            // Точное масштабирование крайнего куска по оси Z
            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / item.stepDistance;
                Vector3 currentScale = permanentSegment.transform.localScale;
                permanentSegment.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);
            }

            // Автоматически добавляем MeshCollider на все меши построенного объекта
            foreach (var renderer in permanentSegment.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.GetComponent<Collider>() == null)
                {
                    renderer.gameObject.AddComponent<MeshCollider>();
                }
            }

            // Спавн реальной физической опоры, если строится мост
            if (!item.snapToTerrain && bridgePillarPrefab != null)
            {
                SpawnPillar(spawnPos, finalContainer.transform, false);
            }
        }
    }

    /// <summary>
    /// Очистка призрачных объектов со сцены
    /// </summary>
    public void ClearGhostRoad()
    {
        if (ghostContainer != null) Destroy(ghostContainer);
    }

    /// <summary>
    /// Метод для спавна и динамического растягивания опор моста строго вниз до земли
    /// </summary>
    private void SpawnPillar(Vector3 segmentPosition, Transform parent, bool isGhost)
    {
        // Пускаем луч из центра сегмента моста строго вниз
        if (Physics.Raycast(segmentPosition, Vector3.down, out RaycastHit hit, 200f, terrainLayer))
        {
            float distanceToGround = hit.distance;

            // Если мост слишком близко к земле (выходит на склон холма), опора не нужна
            if (distanceToGround < 1.5f) return;

            // Находим точку спавна ровно посередине между пролетом моста и землей
            Vector3 pillarSpawnPos = segmentPosition + (Vector3.down * (distanceToGround / 2f));

            GameObject pillar = Instantiate(bridgePillarPrefab, pillarSpawnPos, Quaternion.identity, parent);

            // Масштабируем опору по оси Y, чтобы она коснулась земли
            Vector3 pillarScale = pillar.transform.localScale;
            pillar.transform.localScale = new Vector3(pillarScale.x, distanceToGround / 2f, pillarScale.z);

            if (isGhost)
            {
                // У призрака опоры удаляем коллайдеры
                foreach (var col in pillar.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            else
            {
                // Финальной опоре генерируем физику
                foreach (var renderer in pillar.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.gameObject.GetComponent<Collider>() == null)
                        renderer.gameObject.AddComponent<MeshCollider>();
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод для определения нормали (наклона) земли под точкой
    /// </summary>
    private Vector3 GetTerrainNormalAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainLayer))
        {
            return hit.normal;
        }
        return Vector3.up;
    }
}