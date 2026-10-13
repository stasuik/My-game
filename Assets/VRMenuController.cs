using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class VRMenuController : MonoBehaviour
{
    [Header("References")]
    public GameObject menuCanvas;
    public Transform playerHead;
    public InputActionProperty toggleMenuAction;
    public BuildManager buildManager;
    public Transform buttonsContainer;

    [Header("Menu Settings")]
    public float spawnDistance = 1.5f;
    public float spawnHeightOffset = -0.2f;
    public Vector2 cardSize = new Vector2(180, 80);
    public Vector2 previewImageSize = new Vector2(70, 70);

    [Header("Preview Render")]
    public int previewResolution = 128;

    [Header("Designed Panel")]
    [Tooltip("Меню по макету Сони (вкладки). Выключить, чтобы вернуть старую сетку карточек.")]
    public bool useDesignedPanel = true;

    private BuildMenuPanel designedPanel;

    private List<GameObject> menuButtons = new List<GameObject>();
    private Transform scrollContent;
    private Camera previewCamera;
    private RenderTexture previewRT;
    private readonly Dictionary<int, Texture2D> previewCache = new Dictionary<int, Texture2D>();
    private bool menuBuilt = false;

    private void Awake()
    {
        if (buildManager == null) buildManager = GetComponent<BuildManager>();
        if (buildManager == null) buildManager = FindObjectOfType<BuildManager>();

        if (buttonsContainer == null && menuCanvas != null)
        {
            Transform bc = menuCanvas.transform.Find("ButtonsContainer");
            if (bc != null) buttonsContainer = bc;
        }
    }

    private void OnEnable()
    {
        toggleMenuAction.action.Enable();
        toggleMenuAction.action.started += OnTriggerToggle;

        if (menuCanvas != null)
        {
            menuCanvas.SetActive(false);
            SetupPreviewSystem();
        }
    }

    private void OnDisable()
    {
        toggleMenuAction.action.Disable();
        toggleMenuAction.action.started -= OnTriggerToggle;
        CleanupPreviewSystem();
    }

    private void SetupPreviewSystem()
    {
        CleanupPreviewSystem();

        GameObject camGO = new GameObject("_MenuPreviewCamera");
        camGO.transform.position = new Vector3(0, -9999, 0);
        previewCamera = camGO.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.75f, 0.75f, 0.75f, 1);
        previewCamera.cullingMask = 1 << 5;
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;
        previewCamera.enabled = false;

        GameObject lightGO = new GameObject("_MenuPreviewLight");
        lightGO.transform.SetParent(camGO.transform);
        lightGO.transform.localPosition = new Vector3(3, 5, 2);
        Light l = lightGO.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1f;
        l.shadows = LightShadows.None;

        previewRT = new RenderTexture(previewResolution, previewResolution, 24, RenderTextureFormat.ARGB32);
        previewCamera.targetTexture = previewRT;
    }

    private void CleanupPreviewSystem()
    {
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        if (previewRT != null) { previewRT.Release(); Destroy(previewRT); }
        foreach (var kv in previewCache) Destroy(kv.Value);
        previewCache.Clear();
    }

    private void OnTriggerToggle(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (menuCanvas == null) return;

        if (!menuCanvas.activeSelf)
        {
            if (useDesignedPanel)
            {
                EnsureDesignedPanel();
            }
            else if (!menuBuilt)
            {
                if (buttonsContainer != null && buttonsContainer.childCount > 0)
                    menuBuilt = true; // запечённые кнопки уже в сцене
                else
                    StartCoroutine(BuildMenu());
            }
            PositionMenuInFront();
            menuCanvas.SetActive(true);
        }
        else
        {
            menuCanvas.SetActive(false);
        }
    }

    // Меню по макету Сони (BuildMenuPanel): прячет старую сетку карточек и собирает панель с вкладками
    private void EnsureDesignedPanel()
    {
        if (designedPanel != null) return;

        foreach (Transform child in menuCanvas.transform)
            child.gameObject.SetActive(false);

        RectTransform canvasRT = menuCanvas.GetComponent<RectTransform>();
        if (canvasRT != null)
            canvasRT.sizeDelta = new Vector2(BuildMenuPanel.W, BuildMenuPanel.H);

        GameObject go = new GameObject("BuildMenuPanel", typeof(RectTransform));
        go.layer = menuCanvas.layer;
        go.transform.SetParent(menuCanvas.transform, false);
        designedPanel = go.AddComponent<BuildMenuPanel>();
        designedPanel.Init(buildManager, this);
    }

    public void CloseMenu()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    public void RebuildMenu()
    {
        menuBuilt = false;
        ClearMenu();
        if (menuCanvas != null && menuCanvas.activeSelf)
            StartCoroutine(BuildMenu());
    }

    private IEnumerator BuildMenu()
    {
        ClearMenu();

        if (buildManager == null || buttonsContainer == null) yield break;

        yield return null; // ждём кадр — канвас уже активен

        int totalItems = buildManager.catalog.Count;
        int cols = totalItems <= 2 ? totalItems : 3;
        cols = Mathf.Clamp(cols, 1, totalItems);
        int rows = Mathf.CeilToInt((float)totalItems / cols);

        float spacingX = 12f, spacingY = 12f;
        float padTop = 8f, padBottom = 8f, padSides = 8f;
        float gridW = cols * cardSize.x + (cols - 1) * spacingX;
        float gridH = rows * cardSize.y + (rows - 1) * spacingY;

        RectTransform containerRT = buttonsContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = Vector2.zero;
        containerRT.anchorMax = Vector2.one;
        containerRT.offsetMin = new Vector2(16, 16);
        containerRT.offsetMax = new Vector2(-16, -16);
        containerRT.pivot = new Vector2(0.5f, 0.5f);

        DestroyImmediate(buttonsContainer.GetComponent<GridLayoutGroup>());
        DestroyImmediate(buttonsContainer.GetComponent<ContentSizeFitter>());
        DestroyImmediate(buttonsContainer.GetComponent<Mask>());

        Image img = buttonsContainer.GetComponent<Image>();
        if (img == null) img = buttonsContainer.gameObject.AddComponent<Image>();
        img.color = new Color(0.7f, 0.7f, 0.7f, 0.15f);
        img.raycastTarget = false;

        RectMask2D clip = buttonsContainer.GetComponent<RectMask2D>();
        if (clip == null) clip = buttonsContainer.gameObject.AddComponent<RectMask2D>();

        ScrollRect sr = buttonsContainer.GetComponent<ScrollRect>();
        if (sr == null) sr = buttonsContainer.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.05f;

        // Создаём контент
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(buttonsContainer, false);
        scrollContent = contentGO.transform;
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0, 1);
        contentRT.anchoredPosition = Vector2.zero;

        float availW = 800f - 32f - padSides * 2; // canvas - containerOffset - padding
        float padH = Mathf.Max(0, (availW - gridW) * 0.5f);
        float totalH = gridH + padTop + padBottom;
        contentRT.sizeDelta = new Vector2(0, totalH);

        sr.content = contentRT;

        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = new Vector2(spacingX, spacingY);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.padding = new RectOffset(Mathf.FloorToInt(padSides + padH), Mathf.FloorToInt(padSides + padH),
                                      Mathf.FloorToInt(padTop), Mathf.FloorToInt(padBottom));

        for (int i = 0; i < totalItems; i++)
        {
            var item = buildManager.catalog[i];
            Texture2D tex = item.previewIcon != null ? item.previewIcon.texture as Texture2D : RenderPreview(i);
            CreateCard(item, i, tex);
            yield return null;
        }

        menuBuilt = true;
    }

    private Texture2D RenderPreview(int index)
    {
        if (previewCache.TryGetValue(index, out Texture2D cached))
            return cached;

        var item = buildManager.catalog[index];
        GameObject prefab = item.finalPrefab;
        if (prefab == null) return null;

        GameObject previewObj = Instantiate(prefab);
        previewObj.layer = 5;
        SetLayerRecursively(previewObj, 5);

        Bounds bounds = new Bounds(previewObj.transform.position, Vector3.zero);
        foreach (var r in previewObj.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        float maxDim = Mathf.Max(size.x, size.y, size.z, 0.01f);
        previewCamera.orthographicSize = maxDim * 0.65f;
        float dist = maxDim * 1.8f;

        Vector3 camPos = previewCamera.transform.position;
        previewObj.transform.position = camPos + previewCamera.transform.forward * dist - (center - previewObj.transform.position);
        previewObj.transform.rotation = Quaternion.Euler(25, 135, 0);

        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.75f, 0.75f, 0.75f, 1);

        previewCamera.Render();

        Texture2D tex = new Texture2D(previewResolution, previewResolution, TextureFormat.RGB24, false);
        RenderTexture.active = previewRT;
        tex.ReadPixels(new Rect(0, 0, previewResolution, previewResolution), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Destroy(previewObj);
        previewCache[index] = tex;

        return tex;
    }

    private void CreateCard(BuildableItem item, int index, Texture tex)
    {
        GameObject card = new GameObject("Btn_" + item.itemName, typeof(RectTransform));
        card.transform.SetParent(scrollContent != null ? scrollContent : buttonsContainer, false);

        RectTransform rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = cardSize;

        Image bg = card.AddComponent<Image>();
        bg.sprite = null;
        bg.color = new Color(1, 1, 1, 0.92f);

        Button btn = card.AddComponent<Button>();
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

        int capturedIdx = index;
        btn.onClick.AddListener(() =>
        {
            buildManager.SetBuildMode(capturedIdx);
            CloseMenu();
        });

        if (tex != null)
        {
            GameObject prevGO = new GameObject("Preview", typeof(RectTransform));
            prevGO.transform.SetParent(rt, false);
            RectTransform prevRT = prevGO.GetComponent<RectTransform>();
            prevRT.anchorMin = new Vector2(0, 0.5f);
            prevRT.anchorMax = new Vector2(0, 0.5f);
            prevRT.pivot = new Vector2(0.5f, 0.5f);
            prevRT.anchoredPosition = new Vector2(8 + previewImageSize.x * 0.5f, 0);
            prevRT.sizeDelta = previewImageSize;

            RawImage raw = prevGO.AddComponent<RawImage>();
            raw.texture = tex;
        }

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rt, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        float leftPad = (tex != null ? previewImageSize.x + 16 : 8);
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 1);
        labelRT.offsetMin = new Vector2(leftPad, 2);
        labelRT.offsetMax = new Vector2(-4, -2);

        Text label = labelGO.AddComponent<Text>();
        label.text = item.itemName;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) label.font = font;
        label.fontSize = 14;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.15f, 0.15f, 0.15f);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;

        menuButtons.Add(card);
    }

    private void ClearMenu()
    {
        foreach (var btn in menuButtons)
        {
            if (btn != null) Destroy(btn);
        }
        menuButtons.Clear();

        if (buttonsContainer != null)
        {
            for (int i = buttonsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = buttonsContainer.GetChild(i);
                if (child != null && child.gameObject != null) Destroy(child.gameObject);
            }
        }
    }

    private void PositionMenuInFront()
    {
        if (playerHead == null) return;

        Vector3 headForward = playerHead.forward;
        headForward.y = 0;
        headForward.Normalize();

        Vector3 targetPosition = playerHead.position + (headForward * spawnDistance);
        targetPosition.y += spawnHeightOffset;

        menuCanvas.transform.position = targetPosition;

        menuCanvas.transform.LookAt(new Vector3(playerHead.position.x, targetPosition.y, playerHead.position.z));
        menuCanvas.transform.Rotate(0, 180, 0);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
