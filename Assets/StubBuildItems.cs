using UnityEngine;

/// <summary>
/// Заглушки для объектов, у которых пока нет 3D-моделей: тоннель, эстакада, парковка.
/// Формы собираются из кубов во время игры, ассеты создавать не нужно. Размеры в метрах.
/// BuildMenuPanel подставляет заглушку в каталог, только если в каталоге нет объекта с таким именем.
/// Когда появится настоящая модель, достаточно добавить BuildableItem с именем «Тоннель», «Эстакада» или «Парковка».
/// </summary>
public static class StubBuildItems
{
    static readonly Color Concrete = new Color(0.62f, 0.62f, 0.60f);
    static readonly Color ConcreteDark = new Color(0.45f, 0.45f, 0.44f);
    static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.19f);
    static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color SignBlue = new Color(0.1f, 0.35f, 0.8f);

    public static bool IsStub(string label)
    {
        return label == "Тоннель" || label == "Эстакада" || label == "Парковка";
    }

    public static BuildableItem Create(string label)
    {
        GameObject prefab;
        switch (label)
        {
            case "Тоннель": prefab = BuildTunnel(); break;
            case "Эстакада": prefab = BuildFlyover(); break;
            case "Парковка": prefab = BuildParking(); break;
            default: return null;
        }

        BuildableItem item = ScriptableObject.CreateInstance<BuildableItem>();
        item.name = "Stub_" + label;
        item.itemName = label;
        item.finalPrefab = prefab;
        item.isSplineType = false;
        item.snapToTerrain = true;
        return item;
    }

    // Портал тоннеля: две стены и крыша, внутренняя ширина 7 м, длина 12 м
    static GameObject BuildTunnel()
    {
        GameObject root = NewRoot("_Stub_Tunnel");
        Box(root.transform, "WallLeft", new Vector3(-4f, 3f, 0f), new Vector3(1f, 6f, 12f), Concrete);
        Box(root.transform, "WallRight", new Vector3(4f, 3f, 0f), new Vector3(1f, 6f, 12f), Concrete);
        Box(root.transform, "Roof", new Vector3(0f, 5.5f, 0f), new Vector3(9f, 1f, 12f), ConcreteDark);
        return root;
    }

    // Эстакада: полотно на высоте 5-6 м, бортики и три пары опор, длина 24 м
    static GameObject BuildFlyover()
    {
        GameObject root = NewRoot("_Stub_Flyover");
        Box(root.transform, "Deck", new Vector3(0f, 5.5f, 0f), new Vector3(9f, 1f, 24f), Concrete);
        Box(root.transform, "Surface", new Vector3(0f, 6.02f, 0f), new Vector3(7f, 0.05f, 24f), Asphalt);
        Box(root.transform, "RailLeft", new Vector3(-4.35f, 6.3f, 0f), new Vector3(0.3f, 0.6f, 24f), ConcreteDark);
        Box(root.transform, "RailRight", new Vector3(4.35f, 6.3f, 0f), new Vector3(0.3f, 0.6f, 24f), ConcreteDark);
        float[] zs = { -9f, 0f, 9f };
        foreach (float z in zs)
        {
            Box(root.transform, "PillarL", new Vector3(-3f, 2.5f, z), new Vector3(1f, 5f, 1f), ConcreteDark);
            Box(root.transform, "PillarR", new Vector3(3f, 2.5f, z), new Vector3(1f, 5f, 1f), ConcreteDark);
        }
        return root;
    }

    // Парковка: плита 16 x 9 м, разметка на 6 мест и знак «P»
    static GameObject BuildParking()
    {
        GameObject root = NewRoot("_Stub_Parking");
        Box(root.transform, "Slab", new Vector3(0f, 0.05f, 0f), new Vector3(16f, 0.1f, 9f), Asphalt);
        for (int i = 0; i < 7; i++)
        {
            float x = -7.5f + i * 2.5f;
            Box(root.transform, "Line" + i, new Vector3(x, 0.11f, -1.5f), new Vector3(0.12f, 0.02f, 5f), White);
        }
        Box(root.transform, "LineBack", new Vector3(0f, 0.11f, 1f), new Vector3(15f, 0.02f, 0.12f), White);
        Box(root.transform, "SignPole", new Vector3(-7.5f, 1.5f, 3.8f), new Vector3(0.12f, 3f, 0.12f), ConcreteDark);
        Box(root.transform, "SignPlate", new Vector3(-7.5f, 3.1f, 3.8f), new Vector3(1f, 1f, 0.05f), SignBlue);
        return root;
    }

    // Шаблон лежит далеко под сценой: Instantiate копирует активный объект, а ставится копия в нужную точку
    static GameObject NewRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(0f, -1000f, 0f);
        return root;
    }

    static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().material.color = color;
    }
}
