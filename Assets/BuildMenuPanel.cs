using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Меню строительства по макету Сони.
///
/// Два режима:
///  - обычный: панель собирается кодом (Init -> Build), работает в любой сцене;
///  - baked: панель уже собрана в сцене (инструмент «Стройка/Закрепить панель меню в сцене»),
///    тогда в Play ничего не строится, а только привязывается логика к готовым кнопкам.
///
/// Благодаря этому в закреплённой панели кнопки — обычные объекты сцены:
/// к ним можно привязать свои скрипты прямо в инспекторе.
///
/// Вариант для VR: вкладки, шрифт от 19, кнопки от 44 единиц канваса.
/// Иконки: спрайты в Assets/Resources/UI/Icons/{ключ}.png (road, bridge, flyover, tunnel,
/// parking, lamp, bin, bench, bus, sign, fence, move, trash, anchor). Пока их нет — кружок с буквой.
/// </summary>
public class BuildMenuPanel : MonoBehaviour
{
    // Размеры в единицах канваса (масштаб канваса в сцене 0.002, то есть 600 ед. = 1.2 м)
    public const float W = 600f, H = 520f;
    const float HeaderH = 70f, TabsH = 52f, StatusH = 36f, Pad = 20f;
    const float ContentH = H - HeaderH - TabsH - StatusH;
    const float PaneW = W - Pad * 2f;

    // Палитра макета
    public static readonly Color Cream = Hex("FFF8EC");
    public static readonly Color Line = Hex("F3E6CF");
    public static readonly Color TileCol = Hex("FFE8CC");
    public static readonly Color TileSel = Hex("FFDCB0");
    public static readonly Color SegCol = Hex("FFEFD8");
    public static readonly Color ChipCol = Hex("FFF0DC");
    public static readonly Color IconBg = Hex("FFF3E2");
    public static readonly Color Orange = Hex("FF9F1C");
    public static readonly Color Ink = Hex("3B2B1A");
    public static readonly Color Mute = Hex("8A7558");
    public static readonly Color Faint = Hex("B79C78");
    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    static readonly string[] TabNames = { "Tab_Тип", "Tab_Сегмент", "Tab_Дорога", "Tab_Объекты", "Tab_Инструменты" };
    static readonly string[] PaneNames = { "Pane_Type", "Pane_Segment", "Pane_Road", "Pane_Side", "Pane_Tools" };

    [Tooltip("Панель уже собрана в сцене: в Play ничего не строим, только привязываем логику.")]
    public bool baked;

    [SerializeField] BuildManager buildManager;
    [SerializeField] VRMenuController menu;

    Font font;
    Text crumb, status;
    Image tabLine;
    readonly List<RectTransform> panes = new List<RectTransform>();
    readonly List<Text> tabLabels = new List<Text>();
    readonly List<RectTransform> tabRects = new List<RectTransform>();

    // выбор типа и придорожных объектов в одной группе, чтобы выбрано было что-то одно
    readonly List<BuildMenuChoice> pickGroup = new List<BuildMenuChoice>();
    readonly List<BuildMenuChoice> toolGroup = new List<BuildMenuChoice>();
    readonly List<BuildMenuStubControl> stubs = new List<BuildMenuStubControl>();

    // ---------- жизненный цикл ----------

    public void Init(BuildManager bm, VRMenuController mc)
    {
        buildManager = bm;
        menu = mc;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (!baked)
        {
            RectTransform rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Build(rt);
        }

        Bind();
    }

    void Start()
    {
        if (!baked) return; // процедурный путь всё сделал в Init

        if (menu == null) menu = FindObjectOfType<VRMenuController>();
        if (buildManager == null && menu != null) buildManager = menu.buildManager;
        if (buildManager == null) buildManager = FindObjectOfType<BuildManager>();
        Bind();
    }

    // ---------- сборка (только редактор / процедурный режим) ----------

    void Build(RectTransform root)
    {
        AddImage(root, Cream, Rounded(), true);

        // шапка
        Label(root, 20, 10, 470, 34, "Строительство дорог", 26, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).name = "Title";
        crumb = Label(root, 20, 42, 470, 24, "Проезжая часть > Дорога", 17, Mute, TextAnchor.MiddleLeft);
        crumb.name = "Crumb";
        MakeFlat(root, "Close", W - 64, 13, 44, 44, "X", 20, Ink, TileCol, Circle());
        AddImage(NewPlaced("HeaderLine", root, 0, HeaderH - 1, W, 1), Line, null, false);

        // вкладки
        float tabW = W / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            RectTransform tr = NewPlaced(TabNames[i], root, i * tabW, HeaderH, tabW, TabsH);
            AddImage(tr, Clear, null, true);
            Text t = Label(tr, 0, 0, tabW, TabsH, TabNames[i].Substring(4), 19, Mute, TextAnchor.MiddleCenter);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = 19;
            Button b = tr.gameObject.AddComponent<Button>();
            Flat(b);
        }
        AddImage(NewPlaced("TabsLine", root, 0, HeaderH + TabsH - 1, W, 1), Line, null, false);
        tabLine = AddImage(NewPlaced("TabUnderline", root, 0, HeaderH + TabsH - 4, tabW, 4), Orange, null, false);

        // содержимое
        RectTransform content = NewPlaced("Content", root, 0, HeaderH + TabsH, W, ContentH);
        BuildTypePane(content);
        BuildSegmentPane(content);
        BuildRoadPane(content);
        BuildSidePane(content);
        BuildToolsPane(content);

        // статус
        AddImage(NewPlaced("StatusLine", root, 0, H - StatusH, W, 1), Line, null, false);
        status = Label(root, Pad, H - StatusH, PaneW, StatusH, "Выберите тип объекта. Параметры и инструменты пока заглушки.", 16, Mute, TextAnchor.MiddleLeft);
        status.name = "Status";
        status.resizeTextForBestFit = true;
        status.resizeTextMinSize = 12;
        status.resizeTextMaxSize = 16;
    }

    void BuildTypePane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Type");
        string[,] row1 = { { "road", "Дорога" }, { "bridge", "Мост" }, { "flyover", "Эстакада" } };
        string[,] row2 = { { "tunnel", "Тоннель" }, { "parking", "Парковка" } };
        string[][] kw =
        {
            new[] { "дорог", "road", "doroga" }, new[] { "мост", "bridge", "most" }, new[] { "эстакад" },
            new[] { "тоннел", "туннел", "tunnel" }, new[] { "парков", "parking" }
        };
        float tw = (PaneW - 32f) / 3f;
        for (int i = 0; i < 3; i++)
            MakeTile(pane, i * (tw + 16f), 0, tw, 104, row1[i, 0], row1[i, 1], kw[i], "Проезжая часть");
        float w2 = (PaneW - 16f) / 2f;
        for (int i = 0; i < 2; i++)
            MakeTile(pane, i * (w2 + 16f), 116, w2, 104, row2[i, 0], row2[i, 1], kw[3 + i], "Проезжая часть");
    }

    void BuildSegmentPane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Segment");
        float rowH = 52f, step = 58f, ctrlRight = PaneW;

        Row(pane, 0 * step, rowH, "Длина", null);
        MakeStepper(pane, 0 * step, ctrlRight, "Длина", 48);

        Row(pane, 1 * step, rowH, "Ширина", null);
        MakeStepper(pane, 1 * step, ctrlRight, "Ширина", 7);

        Row(pane, 2 * step, rowH, "Количество полос", null);
        MakeSegment(pane, 2 * step, ctrlRight, 232, new[] { "1", "2", "3", "4" }, 1, false, "Количество полос");

        Row(pane, 3 * step, rowH, "Высота", "для моста / эстакады");
        MakeChip(pane, 3 * step, ctrlRight, 130, "- м", Faint, false);

        Row(pane, 4 * step, rowH, "Направление", "авто, по протягиванию");
        MakeChip(pane, 4 * step, ctrlRight, 150, "СВ, 42°", Ink, true);

        Row(pane, 5 * step, rowH, "Тип покрытия", null);
        MakeSegment(pane, 5 * step, ctrlRight, 300, new[] { "Бетон", "Асфальт", "..." }, 0, true, "Тип покрытия");
    }

    void BuildRoadPane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Road");
        MakeCheck(pane, 0, "Тротуар", true);
        MakeCheck(pane, 64, "Бордюр", false);
        MakeCheck(pane, 128, "Столбы / фонари", false);
    }

    void BuildSidePane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Side");
        string[,] items =
        {
            { "lamp", "Фонарь" }, { "bin", "Урна" }, { "bench", "Лавочка" },
            { "bus", "Остановка" }, { "sign", "Знак" }, { "fence", "Ограждение" }
        };
        string[][] kw =
        {
            new[] { "фонар", "fonar", "lamp" }, new[] { "урн", "мусор", "musor", "urna" }, new[] { "лавоч", "lavoch", "bench" },
            new[] { "останов", "ostanov" }, new[] { "знак", "znak", "sign" }, new[] { "огражд", "забор", "fence" }
        };
        float tw = (PaneW - 32f) / 3f;
        for (int i = 0; i < 6; i++)
            MakeTile(pane, (i % 3) * (tw + 16f), (i / 3) * 116f, tw, 104, items[i, 0], items[i, 1], kw[i], "Придорожные объекты");
    }

    void BuildToolsPane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Tools");
        string[,] items = { { "move", "Переместить" }, { "trash", "Удалить" }, { "anchor", "Точка привязки" } };
        float tw = (PaneW - 32f) / 3f;
        for (int i = 0; i < 3; i++)
            MakeTile(pane, i * (tw + 16f), 0, tw, 104, items[i, 0], items[i, 1], new string[0], "Инструменты");
    }

    // ---------- элементы ----------

    void MakeTile(RectTransform parent, float x, float y, float w, float h, string key, string label,
                  string[] keywords, string crumbGroup)
    {
        RectTransform rt = NewPlaced("Tile_" + label, parent, x, y, w, h);
        Image border = AddImage(rt, Clear, Rounded(), true);
        RectTransform fillRt = NewRect("Fill", rt);
        Stretch(fillRt, 3f);
        Image fill = AddImage(fillRt, TileCol, Rounded(), true);

        MakeIcon(rt, key, label, w * 0.5f - 22f, 12f, 44f);
        Text t = Label(rt, 4, 62, w - 8, 34, label, 19, Mute, TextAnchor.MiddleCenter);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = 19;

        BuildMenuChoice choice = rt.gameObject.AddComponent<BuildMenuChoice>();
        choice.label = label;
        choice.keywords = keywords;
        choice.crumbGroup = crumbGroup;
        choice.kind = BuildMenuChoiceKind.Tile;
        choice.border = border;
        choice.fill = fill;
        choice.text = t;
        choice.fillNormal = TileCol;
        choice.fillSelected = TileSel;
        choice.textNormal = Mute;
        choice.textSelected = Ink;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = fill;
        Tint(b);
    }

    void MakeIcon(RectTransform parent, string key, string label, float x, float y, float size)
    {
        RectTransform rt = NewPlaced("Icon", parent, x, y, size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        Sprite custom = Resources.Load<Sprite>("UI/Icons/" + key);
        if (custom != null)
        {
            img.sprite = custom;
            img.preserveAspect = true;
            img.color = Color.white;
            return;
        }
        img.sprite = Circle();
        img.color = IconBg;
        Text t = Label(rt, 0, 0, size, size, label.Substring(0, 1), 22, Orange, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.raycastTarget = false;
    }

    void Row(RectTransform pane, float y, float h, string main, string sub)
    {
        if (sub == null)
        {
            Label(pane, 0, y, 280, h, main, 21, Ink, TextAnchor.MiddleLeft);
            return;
        }
        Label(pane, 0, y + 2, 280, 28, main, 21, Ink, TextAnchor.MiddleLeft);
        Label(pane, 0, y + 30, 280, 20, sub, 15, Faint, TextAnchor.MiddleLeft);
    }

    void MakeStepper(RectTransform parent, float y, float right, string name, int value)
    {
        float w = 200f;
        RectTransform rt = NewPlaced("Stepper_" + name, parent, right - w, y + 2, w, 48);
        AddImage(rt, ChipCol, Rounded(), true);
        Text val = Label(rt, 52, 0, 96, 48, value + " м", 21, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        MakeFlat(rt, "Minus", 0, 0, 52, 48, "-", 28, Faint, Clear, null);
        MakeFlat(rt, "Plus", 148, 0, 52, 48, "+", 28, Faint, Clear, null);

        BuildMenuStubControl ctrl = rt.gameObject.AddComponent<BuildMenuStubControl>();
        ctrl.mode = BuildMenuStubMode.Stepper;
        ctrl.label = name;
        ctrl.value = value;
        ctrl.valueLabel = val;
        ctrl.suffix = " м";
    }

    void MakeChip(RectTransform parent, float y, float right, float w, string text, Color color, bool strong)
    {
        RectTransform rt = NewPlaced("Chip", parent, right - w, y + 6, w, 40);
        AddImage(rt, ChipCol, Rounded(), false);
        Label(rt, 0, 0, w, 40, text, 20, color, TextAnchor.MiddleCenter, strong ? FontStyle.Bold : FontStyle.Normal);
    }

    void MakeSegment(RectTransform parent, float y, float right, float w, string[] labels, int initial, bool filled, string name)
    {
        float gap = 8f;
        float bw = (w - gap * (labels.Length - 1)) / labels.Length;

        RectTransform box = NewPlaced("Seg_" + name, parent, right - w, y + 1, w, 50);
        BuildMenuStubControl ctrl = box.gameObject.AddComponent<BuildMenuStubControl>();
        ctrl.mode = BuildMenuStubMode.Segment;
        ctrl.label = name;
        ctrl.minValue = 0;
        ctrl.maxValue = labels.Length - 1;

        List<BuildMenuChoice> options = new List<BuildMenuChoice>();
        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform rt = NewPlaced("Opt_" + labels[i], box, i * (bw + gap), 0, bw, 50);
            Image border = AddImage(rt, Clear, Rounded(), true);
            RectTransform fillRt = NewRect("Fill", rt);
            Stretch(fillRt, 3f);
            Image fill = AddImage(fillRt, SegCol, Rounded(), true);
            Text t = Label(rt, 0, 0, bw, 50, labels[i], 20, Mute, TextAnchor.MiddleCenter);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = 20;

            BuildMenuChoice choice = rt.gameObject.AddComponent<BuildMenuChoice>();
            choice.kind = BuildMenuChoiceKind.Segment;
            choice.label = labels[i];
            choice.owner = ctrl;
            choice.index = i;
            choice.border = border;
            choice.fill = fill;
            choice.text = t;
            choice.fillNormal = SegCol;
            choice.fillSelected = filled ? Orange : TileSel;
            choice.textNormal = Mute;
            choice.textSelected = filled ? Color.white : Ink;

            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            Tint(b);
            options.Add(choice);
        }

        ctrl.options = options.ToArray();
        ctrl.Select(initial);
    }

    void MakeCheck(RectTransform parent, float y, string label, bool on)
    {
        RectTransform row = NewPlaced("Check_" + label, parent, 0, y, PaneW, 56);
        AddImage(row, Clear, null, true);
        RectTransform ring = NewPlaced("Ring", row, 0, 12, 32, 32);
        Image ringImg = AddImage(ring, Orange, Circle(), false);
        Image innerImg = AddImage(NewPlaced("Inner", ring, 3, 3, 26, 26), Cream, Circle(), false);
        Image dot = AddImage(NewPlaced("Dot", ring, 9, 9, 14, 14), Orange, Circle(), false);
        Label(row, 48, 0, PaneW - 48, 56, label, 22, Ink, TextAnchor.MiddleLeft);

        BuildMenuStubControl ctrl = row.gameObject.AddComponent<BuildMenuStubControl>();
        ctrl.mode = BuildMenuStubMode.Check;
        ctrl.label = label;
        ctrl.ring = ringImg;
        ctrl.inner = innerImg;
        ctrl.dot = dot;
        ctrl.on = on;
        ctrl.Refresh();

        Button b = row.gameObject.AddComponent<Button>();
        Flat(b);
    }

    // ---------- привязка логики к готовым объектам ----------

    void Bind()
    {
        panes.Clear();
        tabLabels.Clear();
        tabRects.Clear();
        pickGroup.Clear();
        toolGroup.Clear();
        stubs.Clear();

        Transform content = transform.Find("Content");
        if (content == null) return;

        for (int i = 0; i < PaneNames.Length; i++)
        {
            Transform p = content.Find(PaneNames[i]);
            panes.Add(p != null ? (RectTransform)p : null);
        }

        for (int i = 0; i < TabNames.Length; i++)
        {
            Transform t = transform.Find(TabNames[i]);
            tabRects.Add(t != null ? (RectTransform)t : null);
            Text lbl = null;
            if (t != null)
            {
                Transform tx = t.Find("Text");
                if (tx != null) lbl = tx.GetComponent<Text>();
            }
            tabLabels.Add(lbl);
        }

        Transform crumbT = transform.Find("Crumb");
        if (crumbT != null) crumb = crumbT.GetComponent<Text>();
        Transform statusT = transform.Find("Status");
        if (statusT != null) status = statusT.GetComponent<Text>();
        Transform underline = transform.Find("TabUnderline");
        if (underline != null) tabLine = underline.GetComponent<Image>();

        // тип и придорожные объекты делят один выбор, инструменты — отдельную группу
        CollectChoices(panes[0], pickGroup);
        CollectChoices(panes[3], pickGroup);
        CollectChoices(panes[4], toolGroup);

        foreach (Transform pane in panes)
        {
            if (pane == null) continue;
            stubs.AddRange(pane.GetComponentsInChildren<BuildMenuStubControl>(true));
        }

        AttachListeners();

        SelectTab(0);
        if (pickGroup.Count > 0) ApplyChoice(pickGroup, pickGroup[0]);
    }

    static void CollectChoices(RectTransform pane, List<BuildMenuChoice> into)
    {
        if (pane == null) return;
        foreach (BuildMenuChoice c in pane.GetComponentsInChildren<BuildMenuChoice>(true))
            into.Add(c);
    }

    void AttachListeners()
    {
        // вкладки
        for (int i = 0; i < tabRects.Count; i++)
        {
            int idx = i;
            RectTransform tab = tabRects[i];
            if (tab == null) continue;
            Button b = tab.GetComponent<Button>();
            if (b != null) b.onClick.AddListener(() => SelectTab(idx));
        }

        // крестик
        Transform closeT = transform.Find("Close");
        if (closeT != null)
        {
            Button b = closeT.GetComponent<Button>();
            if (b != null) b.onClick.AddListener(() => { if (menu != null) menu.CloseMenu(); });
        }

        // плитки и пункты сегментов
        foreach (Transform pane in panes)
        {
            if (pane == null) continue;
            foreach (BuildMenuChoice c in pane.GetComponentsInChildren<BuildMenuChoice>(true))
            {
                Button b = c.GetComponent<Button>();
                if (b == null) continue;
                BuildMenuChoice captured = c;
                b.onClick.AddListener(() => OnChoiceClicked(captured));
            }
        }

        // заглушки
        foreach (BuildMenuStubControl s in stubs)
        {
            if (s == null) continue;
            if (s.mode == BuildMenuStubMode.Stepper)
            {
                AttachStubButton(s, "Minus", s.Minus);
                AttachStubButton(s, "Plus", s.Plus);
            }
            else if (s.mode == BuildMenuStubMode.Check)
            {
                Button b = s.GetComponent<Button>();
                if (b != null) b.onClick.AddListener(() => { s.Toggle(); WriteStub(s.label); });
            }
        }
    }

    void AttachStubButton(BuildMenuStubControl s, string childName, UnityAction action)
    {
        Transform t = s.transform.Find(childName);
        if (t == null) return;
        Button b = t.GetComponent<Button>();
        if (b == null) return;
        b.onClick.AddListener(() => { action(); WriteStub(s.label); });
    }

    // ---------- поведение ----------

    void SelectTab(int index)
    {
        for (int i = 0; i < panes.Count; i++)
            if (panes[i] != null) panes[i].gameObject.SetActive(i == index);
        for (int i = 0; i < tabLabels.Count; i++)
        {
            bool on = i == index;
            if (tabLabels[i] == null) continue;
            tabLabels[i].color = on ? Ink : Mute;
            tabLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
        if (tabLine == null || index >= tabRects.Count) return;
        RectTransform tab = tabRects[index];
        if (tab == null) return;
        tabLine.rectTransform.anchoredPosition = new Vector2(tab.anchoredPosition.x, tabLine.rectTransform.anchoredPosition.y);
    }

    void ApplyChoice(List<BuildMenuChoice> group, BuildMenuChoice selected)
    {
        foreach (BuildMenuChoice c in group)
        {
            if (c == null) continue;
            c.ApplyStyle(c == selected);
        }
    }

    void OnChoiceClicked(BuildMenuChoice c)
    {
        if (c == null) return;

        if (c.kind == BuildMenuChoiceKind.Segment)
        {
            if (c.owner != null)
            {
                c.owner.Select(c.index);
                WriteStub(c.owner.label);
            }
            return;
        }

        if (c.crumbGroup == "Инструменты")
        {
            ApplyChoice(toolGroup, c);
            OnPick(c.crumbGroup, c);
            return;
        }

        // тип и придорожные объекты делят один выбор
        ApplyChoice(pickGroup, c);
        OnPick(c.crumbGroup, c);
    }

    void OnPick(string group, BuildMenuChoice c)
    {
        if (crumb != null) crumb.text = group + " > " + c.label;
        if (group == "Инструменты")
        {
            WriteStub(c.label);
            return;
        }

        int index;
        BuildableItem item = FindItem(c.label, c.keywords, out index);
        bool isStub = false;
        if (item == null && buildManager != null && buildManager.catalog != null && StubBuildItems.IsStub(c.label))
        {
            // модели ещё нет: подставляем простую форму из кубов, в каталоге она остаётся до конца игры
            item = StubBuildItems.Create(c.label);
            if (item != null)
            {
                buildManager.catalog.Add(item);
                index = buildManager.catalog.Count - 1;
                isStub = true;
            }
        }

        if (item != null && buildManager != null)
        {
            buildManager.SetBuildMode(index);
            if (status != null)
                status.text = (isStub ? "Заглушка в игре: " : "В игре выбрано: ") + item.itemName + ". Закройте панель и стройте.";
        }
        else
        {
            WriteStub(c.label);
        }
    }

    BuildableItem FindItem(string label, string[] keywords, out int index)
    {
        index = -1;
        if (buildManager == null || buildManager.catalog == null) return null;
        string l = label.ToLowerInvariant();
        for (int i = 0; i < buildManager.catalog.Count; i++)
        {
            BuildableItem it = buildManager.catalog[i];
            if (it != null && !string.IsNullOrEmpty(it.itemName) && it.itemName.ToLowerInvariant() == l)
            {
                index = i;
                return it;
            }
        }
        if (keywords == null) return null;
        for (int i = 0; i < buildManager.catalog.Count; i++)
        {
            BuildableItem it = buildManager.catalog[i];
            if (it == null || string.IsNullOrEmpty(it.itemName)) continue;
            string name = it.itemName.ToLowerInvariant();
            foreach (string k in keywords)
            {
                if (name.Contains(k))
                {
                    index = i;
                    return it;
                }
            }
        }
        return null;
    }

    void WriteStub(string what)
    {
        if (status != null) status.text = "Заглушка: «" + what + "» пока не влияет на постройку.";
    }

    // ---------- UI-хелперы ----------

    RectTransform NewPane(RectTransform content, string name)
    {
        RectTransform pane = NewPlaced(name, content, Pad, 14, PaneW, ContentH - 14);
        return pane;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static RectTransform NewPlaced(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, x, y, w, h);
        return rt;
    }

    // положение от левого верхнего угла родителя, y вниз
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Image AddImage(RectTransform rt, Color color, Sprite sprite, bool raycast)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    Text Label(Transform parent, float x, float y, float w, float h, string text, int size, Color color,
               TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        RectTransform rt = NewPlaced("Text", parent, x, y, w, h);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    // плоская кнопка с текстом
    void MakeFlat(RectTransform parent, string name, float x, float y, float w, float h, string text, int size,
                  Color textColor, Color bg, Sprite sprite)
    {
        RectTransform rt = NewPlaced(name, parent, x, y, w, h);
        AddImage(rt, bg, sprite, true);
        Label(rt, 0, 0, w, h, text, size, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button b = rt.gameObject.AddComponent<Button>();
        Flat(b);
    }

    static void Flat(Button b)
    {
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        b.transition = Selectable.Transition.None;
    }

    static void Tint(Button b)
    {
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = new ColorBlock();
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.94f, 0.94f, 1f, 1f);
        cb.pressedColor = new Color(0.82f, 0.82f, 0.9f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
    }

    // ---------- процедурные спрайты ----------

    static Sprite roundedSprite, circleSprite;

    public static Sprite Rounded()
    {
        if (roundedSprite == null)
        {
            roundedSprite = Resources.Load<Sprite>("BuildMenu/UI_Rounded");
            if (roundedSprite == null) roundedSprite = MakeSprite(48, 16, true);
        }
        return roundedSprite;
    }

    public static Sprite Circle()
    {
        if (circleSprite == null)
        {
            circleSprite = Resources.Load<Sprite>("BuildMenu/UI_Circle");
            if (circleSprite == null) circleSprite = MakeSprite(64, 32, false);
        }
        return circleSprite;
    }

    public static Sprite MakeSprite(int size, int radius, bool sliced)
    {
        Texture2D tex = MakeTexture(size, radius);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.Apply(false, true);
        Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    // Читаемая текстура — из неё baker сохраняет PNG-ассеты в Resources.
    public static Texture2D MakeTexture(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, size - radius);
                float cy = Mathf.Clamp(fy, radius, size - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    // После создания ассетов кэш сбрасываем, иначе в панель попадут временные спрайты.
    public static void ClearSpriteCache()
    {
        roundedSprite = null;
        circleSprite = null;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}
