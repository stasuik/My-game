using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

public class VRRoadBuilder : MonoBehaviour
{
    public Vector3 modelRotationOffset = new Vector3(0, 90f, 0);

    [Header("Настройки строительства")]
    public GameObject splineContainerPrefab;
    public GameObject roadSegmentPrefab;
    public LayerMask terrainLayer;

    [Header("Настройки огибания рельефа")]
    public float stepDistance = 2.0f;        // Длина модели дороги
    public float roadHeightOffset = 0.03f;

    private SplineContainer currentSplineContainer;
    private Spline currentSpline;

    // Храним только ОФИЦИАЛЬНО ПОДТВЕРЖДЕННЫЕ опорные точки (без учета текущего положения джойстика)
    private List<Vector3> confirmedPoints = new List<Vector3>();
    private List<GameObject> spawnedSegments = new List<GameObject>();

    public void StartNewRoad(Vector3 startPoint)
    {
        spawnedSegments.Clear();
        confirmedPoints.Clear();

        GameObject newRoadObj = Instantiate(splineContainerPrefab, Vector3.zero, Quaternion.identity);
        currentSplineContainer = newRoadObj.GetComponent<SplineContainer>();
        currentSpline = currentSplineContainer.Spline;

        Vector3 alignedStart = GetTerrainHeightAt(startPoint);
        confirmedPoints.Add(alignedStart);

        // Строим первичный вид
        UpdateSplineGeometry(alignedStart);
    }

    public void AddPointToRoad(Vector3 currentJoystickPoint)
    {
        if (currentSpline == null) return;

        // Точка ландшафта, на которую СЕЙЧАС указывает джойстик
        Vector3 liveTarget = GetTerrainHeightAt(currentJoystickPoint);
        Vector3 lastConfirmed = confirmedPoints[confirmedPoints.Count - 1];

        // Считаем дистанцию по горизонтали от последней зафиксированной точки до джойстика
        float distanceXZ = Vector2.Distance(new Vector2(lastConfirmed.x, lastConfirmed.z),
                                            new Vector2(liveTarget.x, liveTarget.z));

        // ЕСЛИ ДЖОЙСТИК УШЕЛ ДАЛЕКО: фиксируем новые постоянные шаги
        if (distanceXZ >= stepDistance)
        {
            int stepsToFix = Mathf.FloorToInt(distanceXZ / stepDistance);
            Vector3 currentPos = lastConfirmed;

            for (int i = 1; i <= stepsToFix; i++)
            {
                float ratio = (float)i / stepsToFix;
                float nextX = Mathf.Lerp(lastConfirmed.x, liveTarget.x, ratio);
                float nextZ = Mathf.Lerp(lastConfirmed.z, liveTarget.z, ratio);

                Vector3 projectedPoint = GetTerrainHeightAt(new Vector3(nextX, currentPos.y, nextZ));
                confirmedPoints.Add(projectedPoint);
                currentPos = projectedPoint;
            }
        }

        // ВАЖНО: Мы всегда перестраиваем сплайн, передавая туда ТЕКУЩУЮ «живую» точку джойстика
        UpdateSplineGeometry(liveTarget);
    }

    // Полное обновление сплайна и анимированное вытягивание кусков
    private void UpdateSplineGeometry(Vector3 livePoint)
    {
        currentSpline.Clear();

        // 1. Сначала добавляем в сплайн все стационарные подтвержденные точки
        for (int i = 0; i < confirmedPoints.Count; i++)
        {
            AddKnotToSpline(confirmedPoints[i]);
        }

        // 2. Если джойстик сместился от последней точки, добавляем его позицию как временный "хвост"
        Vector3 lastFixed = confirmedPoints[confirmedPoints.Count - 1];
        if (Vector3.Distance(lastFixed, livePoint) > 0.1f)
        {
            AddKnotToSpline(livePoint);
        }

        // 3. Пересчитываем тангенсы сглаживания для всех точек
        for (int i = 0; i < currentSpline.Count; i++)
        {
            currentSpline.SetTangentMode(i, TangentMode.AutoSmooth);
        }

        // 4. Генерируем 3D пропы вдоль обновленного сплайна
        BuildRoadAlongSpline();
    }

    private void AddKnotToSpline(Vector3 worldPosition)
    {
        Vector3 localPos = currentSplineContainer.transform.InverseTransformPoint(worldPosition);
        localPos.y += roadHeightOffset;

        BezierKnot newKnot = new BezierKnot();
        newKnot.Position = localPos;
        currentSpline.Add(newKnot);
    }

    private void BuildRoadAlongSpline()
    {
        if (currentSpline == null || roadSegmentPrefab == null) return;

        foreach (GameObject seg in spawnedSegments)
        {
            Destroy(seg);
        }
        spawnedSegments.Clear();

        float totalLength = currentSpline.GetLength();
        if (totalLength < 0.1f) return;

        // Магия плавности: теперь мы делим общую длину на длину модельки дробно (float)
        float segmentsCountFloat = totalLength / stepDistance;
        int numberOfSegments = Mathf.FloorToInt(segmentsCountFloat);

        // Будем спавнить столько целых кусков, сколько влезает
        for (int i = 0; i < numberOfSegments; i++)
        {
            float distanceOnSpline = (i * stepDistance) + (stepDistance * 0.5f);
            SpawnSegmentAtDistance(distanceOnSpline, totalLength, 1.0f); // Обычный масштаб 1:1
        }

        // КРАСИВЫЙ ХВОСТ: если остался недостроенный кусочек до джойстика, мы его плавно растягиваем!
        float remainder = totalLength - (numberOfSegments * stepDistance);
        if (remainder > 0.1f && numberOfSegments >= 0)
        {
            // Центр последнего растягиваемого куска
            float distanceOnSpline = (numberOfSegments * stepDistance) + (remainder * 0.5f);
            float customScaleZ = remainder / stepDistance; // Вычисляем коэффициент растяжения

            SpawnSegmentAtDistance(distanceOnSpline, totalLength, customScaleZ);
        }
    }

    private void SpawnSegmentAtDistance(float distance, float totalLength, float scaleZMultiplier)
    {
        float t = distance / totalLength;

        float3 localPos, localForward, localUp;
        currentSpline.Evaluate(t, out localPos, out localForward, out localUp);

        Vector3 worldPos = currentSplineContainer.transform.TransformPoint(localPos);
        Vector3 worldForward = currentSplineContainer.transform.TransformDirection(localForward);
        Vector3 worldUp = GetTerrainNormalAt(worldPos);

        Quaternion spawnRot = Quaternion.LookRotation(worldForward.normalized, worldUp);
        spawnRot *= Quaternion.Euler(modelRotationOffset);

        GameObject newSegment = Instantiate(roadSegmentPrefab, worldPos, spawnRot, currentSplineContainer.transform);

        // Применяем масштаб: целые куски будут 1.0, а самый крайний у джойстика будет плавно расти от 0 до 1.0
        Vector3 currentScale = newSegment.transform.localScale;
        newSegment.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleZMultiplier);

        if (newSegment.GetComponent<Collider>() == null)
        {
            newSegment.AddComponent<MeshCollider>();
        }

        spawnedSegments.Add(newSegment);
    }

    private Vector3 GetTerrainHeightAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 100f, originPos.z);
        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 200f, terrainLayer))
        {
            return hit.point;
        }
        return originPos;
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

    public void FinishRoad()
    {
        currentSplineContainer = null;
        currentSpline = null;
        spawnedSegments.Clear();
        confirmedPoints.Clear();
    }
}
