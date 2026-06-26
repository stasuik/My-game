using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class VRRoadBuilder : MonoBehaviour
{
    [Header("Настройки строительства")]
    public GameObject splineContainerPrefab; // Префаб с компонентом SplineContainer
    public LayerMask terrainLayer;           // Слой земли (Terrain)

    private SplineContainer currentSplineContainer;
    private Spline currentSpline;

    // Метод для начала новой дороги
    public void StartNewRoad(Vector3 startPoint)
    {
        GameObject newRoadObj = Instantiate(splineContainerPrefab, Vector3.zero, Quaternion.identity);
        currentSplineContainer = newRoadObj.GetComponent<SplineContainer>();
        currentSpline = currentSplineContainer.Spline;

        // Добавляем самую первую точку
        AddPointToRoad(startPoint);
    }

    // Метод для добавления последующих точек дороги
    public void AddPointToRoad(Vector3 pointPosition)
    {
        if (currentSpline == null) return;

        // Создаем новую точку сплайна (ноду)
        BezierKnot newKnot = new BezierKnot();
        newKnot.Position = pointPosition;

        // Добавляем её в конец линии
        currentSpline.Add(newKnot);

        // Обновляем форму сплайна, чтобы она автоматически сглаживала углы и повороты
        currentSpline.SetTangentMode(currentSpline.Count - 1, TangentMode.AutoSmooth);
    }

    // Завершить строительство текущей дороги
    public void FinishRoad()
    {
        currentSplineContainer = null;
        currentSpline = null;
    }
}

