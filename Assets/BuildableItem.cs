using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Buildable Item", menuName = "Construction/Buildable Item")]
public class BuildableItem : ScriptableObject
{
    public string itemName;

    [Header("Префабы")]
    public GameObject finalPrefab;  // Физический объект
    public GameObject ghostPrefab;  // Прозрачный призрак (если нет, код возьмет finalPrefab)

    [Header("Логика размещения")]
    public bool isSplineType = false;       // true — тянется (дорога, мост), false — ставится в одну точку (остановка)
    public bool snapToTerrain = true;       // true — повторяет рельеф земли, false — строится по прямой в воздухе (для мостов)

    [Header("Параметры шага (только для Spline)")]
    public float stepDistance = 5f;         // Длина одного сегмента
    public float heightOffset = 0.02f;      // Высота над точкой клика
}
