using UnityEngine;

// В какую вкладку меню попадает объект. Auto: сплайновые (дорога, мост) идут в «Тип», остальные в «Объекты».
public enum BuildCategory
{
    Auto,
    Roadway,   // вкладка «Тип»: проезжая часть
    Roadside   // вкладка «Объекты»: придорожные объекты
}

[CreateAssetMenu(fileName = "New Buildable Item", menuName = "Construction/Buildable Item")]
public class BuildableItem : ScriptableObject
{
    public string itemName;
    public Sprite previewIcon;

    [Header("Меню")]
    public BuildCategory category = BuildCategory.Auto;

    [Header("Prefabs")]
    public GameObject finalPrefab;
    public GameObject ghostPrefab;

    [Header("Режим постройки")]
    public bool isSplineType = false;       // true: протягивание (дорога, мост), false: ставится в одну точку
    public bool snapToTerrain = true;       // true: ставится на землю, false: может висеть над землёй (мосты)

    [Header("Протягивание (только для Spline)")]
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

    public BuildCategory ResolvedCategory
    {
        get
        {
            if (category != BuildCategory.Auto) return category;
            return isSplineType ? BuildCategory.Roadway : BuildCategory.Roadside;
        }
    }
}
