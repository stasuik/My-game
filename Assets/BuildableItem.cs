using UnityEngine;

[CreateAssetMenu(fileName = "New Buildable Item", menuName = "Construction/Buildable Item")]
public class BuildableItem : ScriptableObject
{
    public string itemName;
    public Sprite previewIcon;

    [Header("Prefabs")]
    public GameObject finalPrefab;
    public GameObject ghostPrefab;

    [Header("������ ����������")]
    public bool isSplineType = false;       // true � ������� (������, ����), false � �������� � ���� ����� (���������)
    public bool snapToTerrain = true;       // true � ��������� ������ �����, false � �������� �� ������ � ������� (��� ������)

    [Header("��������� ���� (������ ��� Spline)")]
    public float stepDistance = 2.8f;
    public float heightOffset = 0.04f;

    [Header("Rotation")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Road Proximity")]
    public bool requireRoadProximity = false;
    public float maxRoadDistance = 5f;

    [Header("Bridge Settings")]
    public bool generatePillars = true;
    public float pillarWidth = 0.6f;
    public float minPillarHeight = 1.5f;
    public Material pillarMaterial;
    public bool singleSpan = false;
}
