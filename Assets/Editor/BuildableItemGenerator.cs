using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class BuildableItemGenerator : EditorWindow
{
    [MenuItem("Construction/Generate Roadside Items")]
    static void GenerateItems()
    {
        string basePath = "Assets/roadmodels";
        if (!AssetDatabase.IsValidFolder(basePath))
        {
            Debug.LogError("Folder not found: " + basePath);
            return;
        }

        var createdItems = new List<BuildableItem>();
        var dirs = Directory.GetDirectories(basePath);

        foreach (string dir in dirs)
        {
            string relDir = dir.Replace("\\", "/");
            string dirName = Path.GetFileName(dir);

            // Skip if has valid BuildableItem already
            bool hasValid = false;
            foreach (var assetFile in Directory.GetFiles(dir, "*.asset"))
            {
                string relAsset = assetFile.Replace("\\", "/");
                var existing = AssetDatabase.LoadAssetAtPath<BuildableItem>(relAsset);
                if (existing != null && !string.IsNullOrEmpty(existing.itemName))
                {
                    createdItems.Add(existing);
                    hasValid = true;
                    break;
                }
            }
            if (hasValid) continue;

            // Delete empty/unused .asset files
            foreach (var assetFile in Directory.GetFiles(dir, "*.asset"))
            {
                string relAsset = assetFile.Replace("\\", "/");
                var existing = AssetDatabase.LoadAssetAtPath<BuildableItem>(relAsset);
                if (existing == null || string.IsNullOrEmpty(existing.itemName))
                    AssetDatabase.DeleteAsset(relAsset);
            }

            // Find prefab or fbx
            string prefabPath = null;
            string fbxPath = null;
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLower();
                string nameWO = Path.GetFileNameWithoutExtension(f);
                if (ext == ".prefab")
                    prefabPath = f;
                else if (ext == ".fbx" && fbxPath == null)
                    fbxPath = f;
            }

            string srcPath = prefabPath ?? fbxPath;
            if (srcPath == null)
            {
                Debug.LogWarning("No prefab or fbx in " + dirName + " — skipped");
                continue;
            }

            string itemName = DeriveItemName(Path.GetFileNameWithoutExtension(srcPath));
            string relSrc = srcPath.Replace("\\", "/");

            var so = ScriptableObject.CreateInstance<BuildableItem>();
            so.itemName = itemName;
            so.finalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(relSrc);
            so.ghostPrefab = so.finalPrefab;
            so.isSplineType = false;
            so.snapToTerrain = true;
            so.stepDistance = 2.8f;
            so.heightOffset = 0.04f;
            so.requireRoadProximity = true;
            so.maxRoadDistance = 5f;
            so.generatePillars = false;
            so.singleSpan = false;

            string assetName = dirName.ToLower().Replace(" ", "_").Replace("\\", "/") + ".asset";
            string outPath = relDir + "/" + assetName;
            AssetDatabase.CreateAsset(so, outPath);
            createdItems.Add(so);

            Debug.Log("Created: " + outPath);
        }

        // Populate BuildManager.catalog — keep existing, append new, no dupes
        BuildManager bm = FindObjectOfType<BuildManager>();
        if (bm != null)
        {
            var existingNames = new HashSet<string>(bm.catalog
                .Where(i => i != null && !string.IsNullOrEmpty(i.itemName))
                .Select(i => i.itemName));

            foreach (var item in createdItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemName) && !existingNames.Contains(item.itemName))
                {
                    bm.catalog.Add(item);
                    existingNames.Add(item.itemName);
                }
            }

            EditorUtility.SetDirty(bm);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("BuildManager.catalog: " + bm.catalog.Count + " items total (" + createdItems.Count + " new)");
        }
        else
        {
            Debug.LogWarning("No BuildManager found in current scene");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static string DeriveItemName(string name)
    {
        name = name.Replace(" (1)", "").Replace("(1)", "").Trim();

        var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            {"fonar_1", "Фонарь 1"},
            {"fonar_2", "Фонарь 2"},
            {"fonar_3", "Фонарь 3"},
            {"fonar 1", "Фонарь 1"},
            {"fonar 2", "Фонарь 2"},
            {"svetofor", "Светофор"},
            {"mostik", "Мостик"},
            {"baner", "Баннер"},
            {"doroga1", "Дорога 1"},
            {"doroga2", "Дорога 2"},
            {"gruzovik", "Грузовик"},
            {"konus", "Конус"},
            {"lavochka1", "Лавочка 1"},
            {"lavochka2", "Лавочка 2"},
            {"luk", "Люк"},
            {"mashina", "Машина"},
            {"musorka1", "Мусорка 1"},
            {"musorka2", "Мусорка 2"},
            {"musorka3", "Мусорка 3"},
            {"musorka4", "Мусорка 4"},
            {"musorka5", "Мусорка 5"},
            {"ostanovka", "Остановка"},
            {"protector", "Шина"},
            {"shina", "Шина"},
            {"shinamontag", "Шиномонтаж"},
            {"shlagbaum", "Шлагбаум"},
            {"zapravka", "Заправка"},
            {"glavznak", "Знак «Главная дорога»"},
            {"glavznak1", "Знак «Главная дорога»"},
            {"osatovznak", "Знак «Стоп»"},
            {"znak", "Знак 3"},
        };

        if (map.TryGetValue(name, out string mapped))
            return mapped;

        // Fallback: capitalize first letter
        return name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1) : name;
    }
}
