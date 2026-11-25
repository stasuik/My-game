using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Construction > Sync Models To Menu
/// Проходит по папкам с моделями, для каждой папки с .prefab/.fbx находит или создаёт BuildableItem,
/// подставляет превью-иконку и записывает всё в BuildManager.catalog открытой сцены.
/// BuildMenuPanel строит плитки прямо из каталога, так что после синхронизации модели сразу в меню.
/// Запускать повторно можно сколько угодно: существующие ассеты и их настройки не перезаписываются.
/// </summary>
public class BuildableItemGenerator : EditorWindow
{
    // Каждая подпапка этих папок = одна модель
    static readonly string[] ModelRoots =
    {
        "Assets/roadmodels",
        "Assets/actual 3d models/vr-20261026T190500Z-1-001/vr",
    };

    // Отдельные папки с моделью (ищется .prefab/.fbx внутри, включая подпапки)
    static readonly string[] SingleModelFolders =
    {
        "Assets/actual 3d models/vr-20261026T190500Z-1-001/quick-tunnel",
    };

    const string PreviewFolder = "Assets/roadmodels/_previews";

    // имя папки (без номера в начале) -> название в меню
    static readonly Dictionary<string, string> Names = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        {"мост", "Мост"}, {"мостик", "Мостик"}, {"quick-tunnel", "Тоннель"},
        {"фонарь_1", "Фонарь"}, {"фонарь_2", "Фонарь 2"}, {"фонарь_3", "Фонарь 3"},
        {"светофор", "Светофор"}, {"baner", "Баннер"},
        {"doroga1", "Дорога 1"}, {"doroga2", "Дорога 2"},
        {"gruzovik", "Грузовик"}, {"mashina", "Машина"}, {"konus", "Конус"},
        {"lavochka1", "Лавочка 1"}, {"lavochka2", "Лавочка 2"}, {"luk", "Люк"},
        {"musorka1", "Мусорка 1"}, {"musorka2", "Мусорка 2"}, {"musorka3", "Мусорка 3"},
        {"musorka4", "Мусорка 4"}, {"musorka5", "Мусорка 5"},
        {"ostanovka", "Остановка"}, {"shina", "Шина"}, {"shinamontag", "Шиномонтаж"},
        {"shlagbaum", "Шлагбаум"}, {"zapravka", "Заправка"},
        {"znak1", "Знак «Главная дорога»"}, {"znak2", "Знак «Стоп»"}, {"znak3", "Знак 3"},
    };

    // по этим словам объект попадает во вкладку «Тип», остальное во «Объекты»
    static readonly string[] RoadwayWords = { "дорога", "мост", "тоннел", "туннел", "эстакад", "парковк" };

    [MenuItem("Construction/Sync Models To Menu")]
    static void Sync()
    {
        BuildManager bm = Object.FindObjectOfType<BuildManager>();
        if (bm == null)
        {
            Debug.LogError("[Sync] В открытой сцене нет BuildManager");
            return;
        }

        bm.catalog.RemoveAll(i => i == null);
        var names = new HashSet<string>(bm.catalog.Select(i => i.itemName ?? ""), System.StringComparer.OrdinalIgnoreCase);
        int added = 0, created = 0;

        foreach (string folder in ModelFolders())
        {
            BuildableItem item = FindItemInFolder(folder);
            if (item == null)
            {
                string name = DeriveName(Path.GetFileName(folder));
                if (names.Contains(name)) continue; // такая модель уже есть (дубль из другой папки)
                item = CreateItem(folder, name);
                if (item == null) continue;
                created++;
            }

            EnsureCategory(item);
            EnsurePreview(item);

            if (!bm.catalog.Contains(item) && !names.Contains(item.itemName))
            {
                bm.catalog.Add(item);
                names.Add(item.itemName);
                added++;
            }
        }

        foreach (BuildableItem item in bm.catalog)
        {
            EnsureCategory(item);
            EnsurePreview(item);
        }

        EditorUtility.SetDirty(bm);
        EditorSceneManager.MarkSceneDirty(bm.gameObject.scene);
        AssetDatabase.SaveAssets();

        int roadway = bm.catalog.Count(i => i.ResolvedCategory == BuildCategory.Roadway);
        Debug.Log("[Sync] Каталог: " + bm.catalog.Count + " (Тип: " + roadway + ", Объекты: " + (bm.catalog.Count - roadway) +
                  "). Создано ассетов: " + created + ", добавлено в каталог: " + added + ". Сохраните сцену.");
    }

    static IEnumerable<string> ModelFolders()
    {
        foreach (string root in ModelRoots)
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d))
            {
                string rel = dir.Replace("\\", "/");
                if (Path.GetFileName(rel).StartsWith("_")) continue; // _previews и служебные папки
                yield return rel;
            }
        }
        foreach (string folder in SingleModelFolders)
            if (AssetDatabase.IsValidFolder(folder)) yield return folder;
    }

    static BuildableItem FindItemInFolder(string folder)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildableItem", new[] { folder }))
        {
            var item = AssetDatabase.LoadAssetAtPath<BuildableItem>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !string.IsNullOrEmpty(item.itemName)) return item;
        }
        return null;
    }

    static BuildableItem CreateItem(string folder, string name)
    {
        GameObject model = FindModel(folder);
        if (model == null)
        {
            Debug.LogWarning("[Sync] В " + folder + " нет .prefab/.fbx, пропущено");
            return null;
        }

        bool roadway = IsRoadway(name);
        var so = ScriptableObject.CreateInstance<BuildableItem>();
        so.itemName = name;
        so.finalPrefab = model;
        so.ghostPrefab = model;
        so.category = roadway ? BuildCategory.Roadway : BuildCategory.Roadside;
        so.isSplineType = false;          // протягивание включать вручную, если модель — кусок дороги
        so.snapToTerrain = true;
        so.requireRoadProximity = !roadway; // придорожные объекты ставятся только рядом с дорогой
        so.maxRoadDistance = 5f;
        so.generatePillars = false;

        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(folder).ToLower().Replace(" ", "_") + ".asset");
        AssetDatabase.CreateAsset(so, path);
        Debug.Log("[Sync] Создан " + path + " → " + model.name);
        return so;
    }

    // .prefab важнее .fbx; из нескольких берём с самым коротким именем (без « 1», « (1)»)
    static GameObject FindModel(string folder)
    {
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        string pick = files.Where(f => f.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Length).FirstOrDefault()
                      ?? files.Where(f => f.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Length).FirstOrDefault();
        return pick == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(pick.Replace("\\", "/"));
    }

    static void EnsureCategory(BuildableItem item)
    {
        if (item.category != BuildCategory.Auto) return;
        if (!IsRoadway(item.itemName)) return; // Auto и так даст правильную вкладку
        item.category = BuildCategory.Roadway;
        EditorUtility.SetDirty(item);
    }

    static bool IsRoadway(string name)
    {
        string l = (name ?? "").ToLowerInvariant();
        return RoadwayWords.Any(w => l.Contains(w)) && !l.Contains("знак");
    }

    // Превью: готовый PNG из _previews по имени, иначе рендерим через AssetPreview и сохраняем туда же
    static void EnsurePreview(BuildableItem item)
    {
        if (item.previewIcon != null || item.finalPrefab == null) return;

        string png = PreviewFolder + "/" + item.itemName + ".png";
        if (!File.Exists(png))
        {
            Texture2D tex = AssetPreview.GetAssetPreview(item.finalPrefab);
            for (int i = 0; i < 200 && tex == null && AssetPreview.IsLoadingAssetPreview(item.finalPrefab.GetInstanceID()); i++)
            {
                System.Threading.Thread.Sleep(20);
                tex = AssetPreview.GetAssetPreview(item.finalPrefab);
            }
            if (tex == null) return; // Unity ещё не отрисовал, подхватится при следующем запуске
            Directory.CreateDirectory(PreviewFolder);
            File.WriteAllBytes(png, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(png);
        }

        var importer = AssetImporter.GetAtPath(png) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        item.previewIcon = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        EditorUtility.SetDirty(item);
    }

    static string DeriveName(string folderName)
    {
        // «2 фонарь_1» -> «фонарь_1»
        string key = System.Text.RegularExpressions.Regex.Replace(folderName, @"^\d+\s+", "").Trim();
        if (Names.TryGetValue(key, out string mapped)) return mapped;
        return key.Length > 0 ? char.ToUpper(key[0]) + key.Substring(1) : key;
    }
}
