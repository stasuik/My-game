using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    public Vector3 modelRotationOffset = new Vector3(0, 90f, 0); // По умолчанию повернем на 90 градусов
    [Header("Настройки строительства")]
    public GameObject splineContainerPrefab; // Префаб RoadTemplate (теперь просто с SplineContainer)
    public GameObject roadSegmentPrefab;     // СЮДА МЫ ЗАКИНИМ ТВОЙ FBX КУСОЧЕК ДОРОГИ!
    public LayerMask terrainLayer;           // Слой земли (Terrain)

    [Header("Настройки огибания рельефа")]
    public float stepDistance = 2.0f;        // Расстояние между сегментами (равно длине твоей модели)
    public float roadHeightOffset = 0.25f;

    private SplineContainer currentSplineContainer;
    private Spline currentSpline;
    private Vector3 lastConfirmedPoint;

    // Список для хранения созданных 3D-кусочков текущей дороги, чтобы мы могли их обновлять
    private List<GameObject> spawnedSegments = new List<GameObject>();

    public void StartNewRoad(Vector3 startPoint)
    {
        // Очищаем список старой дороги
        spawnedSegments.Clear();

        GameObject newRoadObj = Instantiate(splineContainerPrefab, Vector3.zero, Quaternion.identity);
        currentSplineContainer = newRoadObj.GetComponent<SplineContainer>();
        currentSpline = currentSplineContainer.Spline;

        Vector3 alignedStart = GetTerrainHeightAt(startPoint);
        lastConfirmedPoint = alignedStart;

        AddRawPoint(alignedStart);
    }

    public void AddPointToRoad(Vector3 targetPoint)
    {
        if (currentSpline == null) return;

        Vector3 alignedTarget = GetTerrainHeightAt(targetPoint);
        float distance = Vector3.Distance(lastConfirmedPoint, alignedTarget);

        if (distance < 0.2f) return;

        // Плавно заполняем путь точками по рельефу ландшафта
        if (distance > stepDistance)
        {
            int segmentsCount = Mathf.FloorToInt(distance / stepDistance);
            for (int i = 1; i <= segmentsCount; i++)
            {
                float ratio = (float)i / segmentsCount;
                Vector3 intermediatePoint = Vector3.Lerp(lastConfirmedPoint, alignedTarget, ratio);
                Vector3 snappedToTerrain = GetTerrainHeightAt(intermediatePoint);
                AddRawPoint(snappedToTerrain);
            }
        }
        else
        {
            AddRawPoint(alignedTarget);
        }

        lastConfirmedPoint = alignedTarget;

        // Вручную спавним 3D-модели вместо заглючившего компонента Unity!
        BuildMeshManually();
    }

    private void AddRawPoint(Vector3 position)
    {
        BezierKnot newKnot = new BezierKnot();
        newKnot.Position = position + new Vector3(0, roadHeightOffset, 0);
        currentSpline.Add(newKnot);
        currentSpline.SetTangentMode(currentSpline.Count - 1, TangentMode.Linear);
    }

    // НАШ СОБСТВЕННЫЙ ЖЕСТКИЙ МЕТОД СПАВНА 3D ДОРОГИ
    private void BuildMeshManually()
    {
        if (currentSpline == null || roadSegmentPrefab == null) return;

        // Удаляем старые куски дороги
        foreach (GameObject seg in spawnedSegments)
        {
            Destroy(seg);
        }
        spawnedSegments.Clear();

        // Идем по всем точкам сплайна
        for (int i = 1; i < currentSpline.Count; i++)
        {
            Vector3 posA = currentSpline[i - 1].Position;
            Vector3 posB = currentSpline[i].Position;

            // 1. Центр спавна — строго между точками А и Б
            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);

            // 2. Считаем направление вектора от А к Б (включая высоту Y!)
            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude; // Реальное расстояние с учетом уклона

            if (actualDistance < 0.01f) continue;

            // 3. Создаем вращение, которое смотрит точно на следующую точку (учитывает наклон холма)
            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized);

            // Применяем наш зафиксированный сдвиг на 90 градусов, чтобы дорога не вставала боком
            spawnRot *= Quaternion.Euler(modelRotationOffset);

            // Спавним кусок дороги
            GameObject newSegment = Instantiate(roadSegmentPrefab, spawnPos, spawnRot, currentSplineContainer.transform);

            // 4. УМНОЕ РАСТЯЖЕНИЕ: Подгоняем длину модельки (ось Z) под реальное расстояние между точками
            // По умолчанию шаг stepDistance равен 2. Мы берем текущий масштаб и корректируем его
            Vector3 currentScale = newSegment.transform.localScale;
            float scaleMultiplier = actualDistance / stepDistance;

            // ВНИМАНИЕ: Если в Blender твоя модель вытянута вдоль оси Z, то меняем Z. 
            // Если лесенка была по оси X, замени ниже Z на X.
            newSegment.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);

            // Добавляем коллайдер
            if (newSegment.GetComponent<Collider>() == null)
            {
                newSegment.AddComponent<MeshCollider>();
            }

            spawnedSegments.Add(newSegment);
        }
    }


    private Vector3 GetTerrainHeightAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 100f, terrainLayer))
        {
            return hit.point;
        }
        return originPos;
    }

    public void FinishRoad()
    {
        currentSplineContainer = null;
        currentSpline = null;
        spawnedSegments.Clear();
    }
}
