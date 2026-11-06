using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public class MenuBaker : EditorWindow
{
    const float CardW = 180f;
    const float CardH = 80f;
    const float SpacingX = 12f;
    const float SpacingY = 12f;
    const float PadTop = 8f;
    const float PadBottom = 8f;
    const float PadSides = 8f;

    [MenuItem("Construction/Bake Menu Buttons")]
    static void Bake()
    {
        var menu = FindObjectOfType<VRMenuController>();
        var bm = FindObjectOfType<BuildManager>();

        if (menu == null || bm == null)
        {
            Debug.LogError("VRMenuController and BuildManager required in scene");
            return;
        }

        if (menu.buttonsContainer == null && menu.menuCanvas != null)
            menu.buttonsContainer = menu.menuCanvas.transform.Find("ButtonsContainer");
        if (menu.buttonsContainer == null)
        {
            Debug.LogError("buttonsContainer not found on VRMenuController");
            return;
        }

        if (menu.buildManager == null) menu.buildManager = bm;

        // Clear all children
        while (menu.buttonsContainer.childCount > 0)
            DestroyImmediate(menu.buttonsContainer.GetChild(0).gameObject);

        // ── Preview system ──
        var previewRT = new RenderTexture(128, 128, 24);
        var previewCamGO = new GameObject("_BakerPreviewCam");
        previewCamGO.hideFlags = HideFlags.HideAndDontSave;
        previewCamGO.transform.position = new Vector3(0, -9999, 0);
        var cam = previewCamGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.75f, 0.75f, 0.75f, 1);
        cam.cullingMask = 1 << 5;
        cam.orthographic = true;
        cam.targetTexture = previewRT;
        cam.enabled = false;

        var lightGO = new GameObject("_BakerPreviewLight");
        lightGO.hideFlags = HideFlags.HideAndDontSave;
        lightGO.transform.SetParent(previewCamGO.transform);
        lightGO.transform.localPosition = new Vector3(3, 5, 2);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.shadows = LightShadows.None;

        string previewFolder = "Assets/roadmodels/_previews";
        if (!AssetDatabase.IsValidFolder(previewFolder))
            AssetDatabase.CreateFolder("Assets/roadmodels", "_previews");

        // ── Configure buttonsContainer as scrollable viewport ──
        var containerRT = menu.buttonsContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = Vector2.zero;
        containerRT.anchorMax = Vector2.one;
        containerRT.offsetMin = new Vector2(16, 16);
        containerRT.offsetMax = new Vector2(-16, -16);
        containerRT.pivot = new Vector2(0.5f, 0.5f);

        // Remove old layout / mask components
        foreach (var c in new System.Type[] {
            typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(Mask) })
        {
            var comp = menu.buttonsContainer.GetComponent(c);
            if (comp != null) DestroyImmediate(comp);
        }

        // Background
        var img = menu.buttonsContainer.GetComponent<Image>();
        if (img == null) img = menu.buttonsContainer.gameObject.AddComponent<Image>();
        img.color = new Color(0.7f, 0.7f, 0.7f, 0.15f);
        img.raycastTarget = false;

        // Clip mask
        if (menu.buttonsContainer.GetComponent<RectMask2D>() == null)
            menu.buttonsContainer.gameObject.AddComponent<RectMask2D>();

        // ScrollRect
        var sr = menu.buttonsContainer.GetComponent<ScrollRect>();
        if (sr == null) sr = menu.buttonsContainer.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.05f;

        // ── Create Content child ──
        int totalItems = bm.catalog.Count;
        int cols = totalItems <= 2 ? totalItems : 3;
        cols = Mathf.Clamp(cols, 1, totalItems);
        int rows = Mathf.CeilToInt((float)totalItems / cols);

        float gridW = cols * CardW + (cols - 1) * SpacingX;
        float gridH = rows * CardH + (rows - 1) * SpacingY;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(menu.buttonsContainer, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0, 1);
        contentRT.anchoredPosition = Vector2.zero;

        float availW = 800f - 32f - PadSides * 2;
        float padH = Mathf.Max(0, (availW - gridW) * 0.5f);
        float totalH = gridH + PadTop + PadBottom;
        contentRT.sizeDelta = new Vector2(0, totalH);

        sr.content = contentRT;

        // Grid on Content
        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardW, CardH);
        grid.spacing = new Vector2(SpacingX, SpacingY);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.padding = new RectOffset(
            Mathf.FloorToInt(PadSides + padH),
            Mathf.FloorToInt(PadSides + padH),
            Mathf.FloorToInt(PadTop),
            Mathf.FloorToInt(PadBottom));

        // ── Create buttons ──
        for (int i = 0; i < totalItems; i++)
        {
            var item = bm.catalog[i];

            Texture2D previewTex = null;
            if (item.previewIcon != null)
            {
                previewTex = item.previewIcon.texture as Texture2D;
            }
            else if (item.finalPrefab != null)
            {
                var clone = Instantiate(item.finalPrefab);
                clone.layer = 5;
                SetLayerRecursively(clone.transform, 5);

                var bounds = new Bounds(clone.transform.position, Vector3.zero);
                foreach (var r in clone.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                var center = bounds.center;
                var size = bounds.size;
                float maxDim = Mathf.Max(size.x, size.y, size.z, 0.01f);
                cam.orthographicSize = maxDim * 0.65f;
                clone.transform.position = cam.transform.position + cam.transform.forward * (maxDim * 1.8f) - (center - clone.transform.position);
                clone.transform.rotation = Quaternion.Euler(25, 135, 0);

                cam.Render();

                previewTex = new Texture2D(128, 128, TextureFormat.RGB24, false);
                RenderTexture.active = previewRT;
                previewTex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                previewTex.Apply();
                RenderTexture.active = null;

                DestroyImmediate(clone);

                string safeName = Sanitize(item.itemName);
                string pngPath = $"{previewFolder}/{safeName}.png";
                File.WriteAllBytes(pngPath, previewTex.EncodeToPNG());
                AssetDatabase.ImportAsset(pngPath);

                var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
            }

            // Card
            var card = new GameObject("Btn_" + Sanitize(item.itemName), typeof(RectTransform));
            card.transform.SetParent(contentRT, false);
            var rt = card.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CardW, CardH);

            var bg = card.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.92f);

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = new Color(1, 1, 1, 0.92f),
                highlightedColor = new Color(0.85f, 0.9f, 1, 1),
                pressedColor = new Color(0.6f, 0.75f, 1, 1),
                selectedColor = new Color(0.85f, 0.9f, 1, 1),
                disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f),
                colorMultiplier = 1,
                fadeDuration = 0.1f
            };

            var mib = card.AddComponent<MenuItemButton>();
            mib.buildManager = bm;
            mib.menuController = menu;
            mib.itemIndex = i;

            // Preview image
            if (previewTex != null)
            {
                var prevGO = new GameObject("Preview", typeof(RectTransform));
                prevGO.transform.SetParent(rt, false);
                var prevRT = prevGO.GetComponent<RectTransform>();
                prevRT.anchorMin = new Vector2(0, 0.5f);
                prevRT.anchorMax = new Vector2(0, 0.5f);
                prevRT.pivot = new Vector2(0.5f, 0.5f);
                prevRT.anchoredPosition = new Vector2(8 + 35, 0);
                prevRT.sizeDelta = new Vector2(70, 70);

                var raw = prevGO.AddComponent<RawImage>();
                string safeName = Sanitize(item.itemName);
                string texPath = $"{previewFolder}/{safeName}.png";
                var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (loadedTex != null) raw.texture = loadedTex;
            }

            // Label
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(rt, false);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(86, 2);
            labelRT.offsetMax = new Vector2(-4, -2);

            var label = labelGO.AddComponent<Text>();
            label.text = item.itemName;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) label.font = font;
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.15f, 0.15f, 0.15f);
        }

        // Cleanup
        DestroyImmediate(previewCamGO);
        previewRT.Release();
        DestroyImmediate(previewRT);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();
        Debug.Log($"Baked {totalItems} menu buttons with scroll support");
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursively(c, layer);
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
