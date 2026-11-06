using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Меню строительства по макету Сони. Собирается кодом внутри мирового канваса.
/// Плитки вкладок «Тип» и «Объекты» создаются из BuildManager.catalog при первом открытии меню:
/// каждый BuildableItem = одна плитка, клик по ней вызывает BuildManager.SetBuildMode(индекс в каталоге).
/// Вкладка задаётся полем BuildableItem.category. Заполнить каталог: Construction > Sync Models To Menu.
/// Параметры сегмента, настройки дороги и инструменты пока заглушки, пишут пояснение в строку статуса.
/// Вариант под VR: вкладки, шрифт от 19, кнопки от 44 единиц канваса.
/// Иконки: BuildableItem.previewIcon, иначе спрайты Assets/Resources/UI/Icons/{ключ}.png (road, bridge, flyover, tunnel,
/// parking, lamp, bin, bench, bus, sign, fence, move, trash, anchor). Пока их нет, рисуется кружок с буквой.
/// </summary>
public class BuildMenuPanel : MonoBehaviour
{
    // Размеры в единицах канваса (масштаб канваса в сцене 0.002, то есть 600 ед. = 1.2 м)
    public const float W = 600f, H = 520f;
    const float HeaderH = 70f, TabsH = 52f, StatusH = 36f, Pad = 20f;
    const float ContentH = H - HeaderH - TabsH - StatusH;
    const float PaneW = W - Pad * 2f;

    // Палитра макета
    static readonly Color Cream = Hex("FFF8EC");
    static readonly Color Line = Hex("F3E6CF");
    static readonly Color TileCol = Hex("FFE8CC");
    static readonly Color TileSel = Hex("FFDCB0");
    static readonly Color SegCol = Hex("FFEFD8");
    static readonly Color ChipCol = Hex("FFF0DC");
    static readonly Color IconBg = Hex("FFF3E2");
    static readonly Color Orange = Hex("FF9F1C");
    static readonly Color Ink = Hex("3B2B1A");
    static readonly Color Mute = Hex("8A7558");
    static readonly Color Faint = Hex("B79C78");
    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    // плитка меню: либо объект из каталога, либо заглушка (stubLabel), либо инструмент
    class Entry
    {
        public string label;
        public string iconKey;
        public BuildableItem item;
        public string stubLabel;
    }

    class Choice
    {
        public string label;
        public Entry entry;
        public Image border, fill;
        public Text text;
        public Color fillNormal, fillSelected, textNormal, textSelected;
    }

    BuildManager buildManager;
    VRMenuController menu;
    Font font;
    Text crumb, status;
    Image tabLine;
    readonly List<RectTransform> panes = new List<RectTransform>();
    readonly List<Text> tabLabels = new List<Text>();
    readonly List<RectTransform> tabRects = new List<RectTransform>();

    // выбор типа и придорожных объектов в одной группе, чтобы выбрано было что-то одно
    readonly List<Choice> pickGroup = new List<Choice>();

    public void Init(BuildManager bm, VRMenuController mc)
    {
        buildManager = bm;
        menu = mc;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Build(rt);
    }

    // ---------- сборка ----------

    void Build(RectTransform root)
    {
        AddImage(root, Cream, Rounded(), true);

        // шапка
        Label(root, 20, 10, 470, 34, "Строительство дорог", 26, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        crumb = Label(root, 20, 42, 470, 24, "Проезжая часть > Дорога", 17, Mute, TextAnchor.MiddleLeft);
        MakeFlat(root, "Close", W - 64, 13, 44, 44, "X", 20, Ink, TileCol, Circle(), () => menu.CloseMenu());
        AddImage(NewPlaced("HeaderLine", root, 0, HeaderH - 1, W, 1), Line, null, false);

        // вкладки
        string[] tabs = { "Тип", "Сегмент", "Дорога", "Объекты", "Инструменты" };
        float tabW = W / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            RectTransform tr = NewPlaced("Tab_" + tabs[i], root, i * tabW, HeaderH, tabW, TabsH);
            AddImage(tr, Clear, null, true);
            Text t = Label(tr, 0, 0, tabW, TabsH, tabs[i], 19, Mute, TextAnchor.MiddleCenter);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = 19;
            Button b = tr.gameObject.AddComponent<Button>();
            Flat(b);
            b.onClick.AddListener(() => SelectTab(idx));
            tabLabels.Add(t);
            tabRects.Add(tr);
        }
        AddImage(NewPlaced("TabsLine", root, 0, HeaderH + TabsH - 1, W, 1), Line, null, false);
        tabLine = AddImage(NewPlaced("TabUnderline", root, 0, HeaderH + TabsH - 4, tabW, 4), Orange, null, false);

        // содержимое
        RectTransform content = NewPlaced("Content", root, 0, HeaderH + TabsH, W, ContentH);
        panes.Add(BuildTypePane(content));
        panes.Add(BuildSegmentPane(content));
        panes.Add(BuildRoadPane(content));
        panes.Add(BuildSidePane(content));
        panes.Add(BuildToolsPane(content));

        // статус
        AddImage(NewPlaced("StatusLine", root, 0, H - StatusH, W, 1), Line, null, false);
        status = Label(root, Pad, H - StatusH, PaneW, StatusH, "Выберите тип объекта. Параметры и инструменты пока заглушки.", 16, Mute, TextAnchor.MiddleLeft);
        status.resizeTextForBestFit = true;
        status.resizeTextMinSize = 12;
        status.resizeTextMaxSize = 16;

        SelectTab(0);
        // первый объект вкладки «Тип» (обычно «Дорога») выбран сразу, как в макете
        Choice first = pickGroup.Find(c => c.entry.item != null);
        if (first != null)
        {
            ApplyChoice(pickGroup, first);
            OnPick("Проезжая часть", first);
        }
    }

    // Вкладка «Тип»: все объекты каталога с категорией Roadway + заглушки, для которых ещё нет модели
    RectTransform BuildTypePane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Type");
        List<Entry> entries = CatalogEntries(BuildCategory.Roadway);
        foreach (string stub in new[] { "Эстакада", "Тоннель", "Парковка" })
        {
            if (entries.Exists(e => e.label.ToLowerInvariant() == stub.ToLowerInvariant())) continue;
            entries.Add(new Entry { label = stub, iconKey = IconKey(stub), stubLabel = stub });
        }
        MakeGrid(pane, entries, "Проезжая часть");
        return pane;
    }

    RectTransform BuildSegmentPane(RectTransform content)
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
        return pane;
    }

    RectTransform BuildRoadPane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Road");
        MakeCheck(pane, 0, "Тротуар", true);
        MakeCheck(pane, 64, "Бордюр", false);
        MakeCheck(pane, 128, "Столбы / фонари", false);
        return pane;
    }

    // Вкладка «Объекты»: все объекты каталога с категорией Roadside, по 6 на страницу
    RectTransform BuildSidePane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Side");
        MakeGrid(pane, CatalogEntries(BuildCategory.Roadside), "Придорожные объекты");
        return pane;
    }

    RectTransform BuildToolsPane(RectTransform content)
    {
        RectTransform pane = NewPane(content, "Pane_Tools");
        string[,] items = { { "move", "Переместить" }, { "trash", "Удалить" }, { "anchor", "Точка привязки" } };
        List<Choice> toolGroup = new List<Choice>();
        float tw = (PaneW - 32f) / 3f;
        for (int i = 0; i < 3; i++)
            MakeTile(pane, i * (tw + 16f), 0, tw, 104, new Entry { label = items[i, 1], iconKey = items[i, 0] }, toolGroup, "Инструменты");
        return pane;
    }

    // ---------- элементы ----------

    void MakeTile(RectTransform parent, float x, float y, float w, float h, Entry entry,
                  List<Choice> group, string crumbGroup)
    {
        string label = entry.label;
        RectTransform rt = NewPlaced(label, parent, x, y, w, h);
        Image border = AddImage(rt, Clear, Rounded(), true);
        RectTransform fillRt = NewRect("Fill", rt);
        Stretch(fillRt, 3f);
        Image fill = AddImage(fillRt, TileCol, Rounded(), true);

        MakeIcon(rt, entry, w * 0.5f - 22f, 12f, 44f);
        Text t = Label(rt, 4, 62, w - 8, 34, label, 19, Mute, TextAnchor.MiddleCenter);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = 19;

        Choice c = new Choice
        {
            label = label, entry = entry, border = border, fill = fill, text = t,
            fillNormal = TileCol, fillSelected = TileSel, textNormal = Mute, textSelected = Ink
        };
        group.Add(c);

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = fill;
        Tint(b);
        b.onClick.AddListener(() =>
        {
            // тип и придорожные объекты делят один выбор, инструменты отдельный
            List<Choice> g = group;
            if (crumbGroup != "Инструменты") g = pickGroup;
            ApplyChoice(g, c);
            OnPick(crumbGroup, c);
        });
    }

    void MakeIcon(RectTransform parent, Entry entry, float x, float y, float size)
    {
        string label = entry.label;
        RectTransform rt = NewPlaced("Icon", parent, x, y, size, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        // приоритет: превью модели из BuildableItem, затем иконка из Resources, затем кружок с буквой
        Sprite custom = entry.item != null && entry.item.previewIcon != null ? entry.item.previewIcon : null;
        if (custom == null && !string.IsNullOrEmpty(entry.iconKey)) custom = Resources.Load<Sprite>("UI/Icons/" + entry.iconKey);
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

    void MakeStepper(RectTransform pane, float y, float right, string name, int value)
    {
        float w = 200f;
        RectTransform rt = NewPlaced("Stepper_" + name, pane, right - w, y + 2, w, 48);
        AddImage(rt, ChipCol, Rounded(), true);
        Text val = Label(rt, 52, 0, 96, 48, value + " м", 21, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        MakeFlat(rt, "Minus", 0, 0, 52, 48, "-", 28, Faint, Clear, null, () =>
        {
            value = Mathf.Max(1, value - 1);
            val.text = value + " м";
            Stub(name);
        });
        MakeFlat(rt, "Plus", 148, 0, 52, 48, "+", 28, Faint, Clear, null, () =>
        {
            value = Mathf.Min(999, value + 1);
            val.text = value + " м";
            Stub(name);
        });
    }

    void MakeChip(RectTransform pane, float y, float right, float w, string text, Color color, bool strong)
    {
        RectTransform rt = NewPlaced("Chip", pane, right - w, y + 6, w, 40);
        AddImage(rt, ChipCol, Rounded(), false);
        Label(rt, 0, 0, w, 40, text, 20, color, TextAnchor.MiddleCenter, strong ? FontStyle.Bold : FontStyle.Normal);
    }

    void MakeSegment(RectTransform pane, float y, float right, float w, string[] labels, int initial, bool filled, string name)
    {
        List<Choice> group = new List<Choice>();
        float gap = 8f;
        float bw = (w - gap * (labels.Length - 1)) / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform rt = NewPlaced("Seg_" + labels[i], pane, right - w + i * (bw + gap), y + 1, bw, 50);
            Image border = AddImage(rt, Clear, Rounded(), true);
            RectTransform fillRt = NewRect("Fill", rt);
            Stretch(fillRt, 3f);
            Image fill = AddImage(fillRt, SegCol, Rounded(), true);
            Text t = Label(rt, 0, 0, bw, 50, labels[i], 20, Mute, TextAnchor.MiddleCenter);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = 20;

            Choice c = new Choice
            {
                label = labels[i], border = border, fill = fill, text = t,
                fillNormal = SegCol, fillSelected = filled ? Orange : TileSel,
                textNormal = Mute, textSelected = filled ? Color.white : Ink
            };
            group.Add(c);

            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            Tint(b);
            b.onClick.AddListener(() =>
            {
                ApplyChoice(group, c);
                Stub(name);
            });
        }
        ApplyChoice(group, group[Mathf.Clamp(initial, 0, group.Count - 1)]);
    }

    void MakeCheck(RectTransform pane, float y, string label, bool on)
    {
        RectTransform row = NewPlaced("Check_" + label, pane, 0, y, PaneW, 56);
        AddImage(row, Clear, null, true);
        RectTransform ring = NewPlaced("Ring", row, 0, 12, 32, 32);
        Image ringImg = AddImage(ring, Orange, Circle(), false);
        Image innerImg = AddImage(NewPlaced("Inner", ring, 3, 3, 26, 26), Cream, Circle(), false);
        Image dot = AddImage(NewPlaced("Dot", ring, 9, 9, 14, 14), Orange, Circle(), false);
        Label(row, 48, 0, PaneW - 48, 56, label, 22, Ink, TextAnchor.MiddleLeft);

        Action refresh = () =>
        {
            ringImg.color = on ? Orange : TileSel;
            innerImg.color = on ? TileCol : Cream;
            dot.enabled = on;
        };
        refresh();

        Button b = row.gameObject.AddComponent<Button>();
        Flat(b);
        b.onClick.AddListener(() =>
        {
            on = !on;
            refresh();
            Stub(label);
        });
    }

    // ---------- поведение ----------

    void SelectTab(int index)
    {
        for (int i = 0; i < panes.Count; i++) panes[i].gameObject.SetActive(i == index);
        for (int i = 0; i < tabLabels.Count; i++)
        {
            bool on = i == index;
            tabLabels[i].color = on ? Ink : Mute;
            tabLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
        RectTransform tl = tabLine.rectTransform;
        RectTransform tab = tabRects[index];
        tl.anchoredPosition = new Vector2(tab.anchoredPosition.x, tl.anchoredPosition.y);
    }

    void ApplyChoice(List<Choice> group, Choice selected)
    {
        foreach (Choice c in group)
        {
            bool on = c == selected;
            c.border.color = on ? Orange : Clear;
            c.fill.color = on ? c.fillSelected : c.fillNormal;
            c.text.color = on ? c.textSelected : c.textNormal;
            c.text.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    void OnPick(string group, Choice c)
    {
        crumb.text = group + " > " + c.label;
        if (group == "Инструменты")
        {
            Stub(c.label);
            return;
        }

        if (buildManager == null || buildManager.catalog == null)
        {
            Stub(c.label);
            return;
        }

        BuildableItem item = c.entry.item;
        bool isStub = false;
        if (item == null && c.entry.stubLabel != null)
        {
            // модели ещё нет: подставляем простую форму из кубов, в каталоге она остаётся до конца игры
            item = StubBuildItems.Create(c.entry.stubLabel);
            if (item != null)
            {
                buildManager.catalog.Add(item);
                c.entry.item = item;
                isStub = true;
            }
        }

        int index = item != null ? buildManager.catalog.IndexOf(item) : -1;
        if (index >= 0)
        {
            buildManager.SetBuildMode(index);
            status.text = (isStub ? "Заглушка в игре: " : "В игре выбрано: ") + item.itemName + ". Закройте панель и стройте.";
        }
        else
        {
            Stub(c.label);
        }
    }

    // ---------- каталог -> плитки ----------

    // Плитки строятся из BuildManager.catalog: добавил BuildableItem в каталог, и он появился в меню
    List<Entry> CatalogEntries(BuildCategory category)
    {
        List<Entry> list = new List<Entry>();
        if (buildManager == null || buildManager.catalog == null) return list;
        foreach (BuildableItem it in buildManager.catalog)
        {
            if (it == null || it.ResolvedCategory != category) continue;
            string label = string.IsNullOrEmpty(it.itemName) ? it.name : it.itemName;
            list.Add(new Entry { label = label, iconKey = IconKey(label), item = it });
        }
        return list;
    }

    // ключ иконки в Resources/UI/Icons по имени объекта
    static string IconKey(string label)
    {
        string l = label.ToLowerInvariant();
        string[,] map =
        {
            { "эстакад", "flyover" }, { "мост", "bridge" }, { "тоннел", "tunnel" }, { "туннел", "tunnel" },
            { "парков", "parking" }, { "дорог", "road" }, { "фонар", "lamp" }, { "светофор", "lamp" },
            { "урн", "bin" }, { "мусор", "bin" }, { "лавоч", "bench" }, { "останов", "bus" },
            { "знак", "sign" }, { "огражд", "fence" }, { "шлагбаум", "fence" }
        };
        for (int i = 0; i < map.GetLength(0); i++)
            if (l.Contains(map[i, 0])) return map[i, 1];
        return null;
    }

    // Сетка 3 x 2 со страницами: в VR кнопки «<» и «>» надёжнее прокрутки
    void MakeGrid(RectTransform pane, List<Entry> entries, string crumbGroup)
    {
        const int perPage = 6;
        float tw = (PaneW - 32f) / 3f;
        int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)perPage));
        List<RectTransform> pageRects = new List<RectTransform>();

        for (int p = 0; p < pages; p++)
        {
            RectTransform page = NewPlaced("Page_" + p, pane, 0, 0, PaneW, 220);
            pageRects.Add(page);
            for (int k = 0; k < perPage; k++)
            {
                int i = p * perPage + k;
                if (i >= entries.Count) break;
                MakeTile(page, (k % 3) * (tw + 16f), (k / 3) * 116f, tw, 104, entries[i], pickGroup, crumbGroup);
            }
        }

        if (entries.Count == 0)
            Label(pane, 0, 0, PaneW, 104, "В каталоге нет объектов. Construction > Sync Models To Menu", 18, Faint, TextAnchor.MiddleCenter);

        if (pages <= 1) return;

        int current = 0;
        Text counter = Label(pane, 0, 240, PaneW, 48, "", 19, Mute, TextAnchor.MiddleCenter, FontStyle.Bold);
        Action show = () =>
        {
            for (int p = 0; p < pageRects.Count; p++) pageRects[p].gameObject.SetActive(p == current);
            counter.text = (current + 1) + " / " + pages;
        };
        MakeFlat(pane, "PrevPage", 0, 240, 120, 48, "<", 26, Ink, TileCol, Rounded(), () =>
        {
            current = (current + pages - 1) % pages;
            show();
        });
        MakeFlat(pane, "NextPage", PaneW - 120, 240, 120, 48, ">", 26, Ink, TileCol, Rounded(), () =>
        {
            current = (current + 1) % pages;
            show();
        });
        show();
    }

    void Stub(string what)
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
                  Color textColor, Color bg, Sprite sprite, Action onClick)
    {
        RectTransform rt = NewPlaced(name, parent, x, y, w, h);
        AddImage(rt, bg, sprite, true);
        Label(rt, 0, 0, w, h, text, size, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button b = rt.gameObject.AddComponent<Button>();
        Flat(b);
        b.onClick.AddListener(() => onClick());
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

    static Sprite Rounded()
    {
        if (roundedSprite == null) roundedSprite = MakeSprite(48, 16, true);
        return roundedSprite;
    }

    static Sprite Circle()
    {
        if (circleSprite == null) circleSprite = MakeSprite(64, 32, false);
        return circleSprite;
    }

    static Sprite MakeSprite(int size, int radius, bool sliced)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
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
        tex.Apply(false, true);
        Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}
