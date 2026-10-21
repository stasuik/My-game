using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Инструмент «закрепить панель меню в сцене».
///
/// Собирает ту же панель, что и BuildMenuPanel.Build, но в виде обычных объектов сцены:
/// после этого кнопки — обычные Button/Image/Text, к ним можно привязать свои скрипты
/// прямо в инспекторе. В Play ничего не строится заново, только привязывается логика.
///
/// Спрайты панели сохраняются как ассеты (Resources/BuildMenu), иначе ссылки на
/// временные спрайты потерялись бы при сохранении сцены.
/// </summary>
public static class BuildMenuBaker
{
    const string AssetFolder = "Assets/Resources/BuildMenu";

    [MenuItem("Tools/Стройка/Закрепить панель меню в сцене")]
    public static void Bake()
    {
        VRMenuController controller = Object.FindObjectOfType<VRMenuController>();
        if (controller == null || controller.menuCanvas == null)
        {
            EditorUtility.DisplayDialog("Меню строительства", "В сцене нет VRMenuController с канвасом меню.", "Ок");
            return;
        }

        GameObject canvas = controller.menuCanvas;
        Transform existing = canvas.transform.Find("BuildMenuPanel");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("Меню строительства",
                "Панель уже закреплена в сцене. Пересобрать её? Привязки скриптов слетят.", "Пересобрать", "Отмена");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Transform preview = canvas.transform.Find("BuildMenuPanelPreview");
        if (preview != null) Undo.DestroyObjectImmediate(preview.gameObject);

        BuildManager bm = controller.buildManager;
        if (bm == null) bm = controller.GetComponent<BuildManager>();
        if (bm == null) bm = Object.FindObjectOfType<BuildManager>();

        EnsureSprites();

        GameObject go = new GameObject("BuildMenuPanel", typeof(RectTransform));
        go.layer = canvas.layer;
        Undo.RegisterCreatedObjectUndo(go, "Закрепить панель меню");
        go.transform.SetParent(canvas.transform, false);

        BuildMenuPanel panel = go.AddComponent<BuildMenuPanel>();
        panel.Init(bm, controller);
        panel.baked = true;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        if (canvasRT != null) canvasRT.sizeDelta = new Vector2(BuildMenuPanel.W, BuildMenuPanel.H);

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;

        Debug.Log("Панель меню закреплена в сцене: " + go.name);
        EditorUtility.DisplayDialog("Меню строительства",
            "Панель закреплена в сцене.\n\nКнопки — обычные объекты: выбирай их в иерархии и привязывай свои скрипты в инспекторе.\nНе забудь сохранить сцену (Ctrl+S).",
            "Ок");
    }

    [MenuItem("Tools/Стройка/Убрать панель меню из сцены")]
    public static void Unbake()
    {
        VRMenuController controller = Object.FindObjectOfType<VRMenuController>();
        if (controller == null || controller.menuCanvas == null) return;

        Transform baked = controller.menuCanvas.transform.Find("BuildMenuPanel");
        if (baked == null)
        {
            EditorUtility.DisplayDialog("Меню строительства", "Закреплённой панели в сцене нет.", "Ок");
            return;
        }

        Undo.DestroyObjectImmediate(baked.gameObject);
        EditorSceneManager.MarkSceneDirty(controller.menuCanvas.scene);
        EditorUtility.DisplayDialog("Меню строительства", "Панель убрана из сцены: меню снова собирается кодом.", "Ок");
    }

    static void EnsureSprites()
    {
        if (!Directory.Exists(AssetFolder)) Directory.CreateDirectory(AssetFolder);

        EnsureSprite(AssetFolder + "/UI_Rounded.png", 48, 16, new Vector4(16, 16, 16, 16));
        EnsureSprite(AssetFolder + "/UI_Circle.png", 64, 32, Vector4.zero);

        BuildMenuPanel.ClearSpriteCache();
        AssetDatabase.Refresh();

        if (Resources.Load<Sprite>("BuildMenu/UI_Rounded") == null || Resources.Load<Sprite>("BuildMenu/UI_Circle") == null)
            Debug.LogWarning("Спрайты панели не загрузились из Resources/BuildMenu — скругления могут не сохраниться в сцене.");
    }

    static void EnsureSprite(string path, int size, int radius, Vector4 border)
    {
        if (!File.Exists(path))
        {
            Texture2D tex = BuildMenuPanel.MakeTexture(size, radius);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (png == null) return;
            File.WriteAllBytes(path, png);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spriteBorder = border;
        ti.spritePivot = new Vector2(0.5f, 0.5f);
        ti.spritePixelsPerUnit = 100f;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }
}
