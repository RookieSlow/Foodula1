using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor 工具：一键生成 RaceCanvas Prefab，含完整 UI 层级、组件引用、按钮绑定。
/// 菜单: Foodula1 → Build RaceCanvas Prefab
/// </summary>
public static class RaceCanvasBuilder
{
    private const string PREFAB_PATH = "Assets/Prefabs/UI/RaceCanvas.prefab";
    private const string CARD_PREFAB_PATH = "Assets/Prefab/CardPrefab.prefab";
    private const string FONT_SDF_GUID = "5358f61b11e22f34e9fe8942033cf67b"; // SourceHanSansTC-Medium-HQ

    [MenuItem("Tools/Build RaceCanvas Prefab")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null)
            AssetDatabase.DeleteAsset(PREFAB_PATH);

        // ── Canvas Root ──
        GameObject root = NewGO("RaceCanvas", null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.localScale = Vector3.one; // 防止序列化为 (0,0,0)
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ── HUD ──
        GameObject hudGO = NewGO("HUD", root, typeof(HUDUI));
        hudGO.GetComponent<RectTransform>().StretchFull();
        var hud = hudGO.GetComponent<HUDUI>();
        hud.maxLogLines = 12;

        BuildHUD(hudGO, hud);

        // ── CardHand ──
        GameObject chGO = NewGO("CardHand", root, typeof(CardHandUI));
        chGO.GetComponent<RectTransform>().StretchFull();
        var ch = chGO.GetComponent<CardHandUI>();

        BuildCardHand(chGO, ch);

        // ── Save Prefab ──
        EnsureDir("Assets/Prefabs/UI");
        PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        Object.DestroyImmediate(root);

        Debug.Log("<color=green>✓ RaceCanvas Prefab 已生成！</color> 拖到 MVPGameManager → Race Canvas Prefab");
    }

    // ================================================================
    //  LAYOUT: 所有 anchoredPosition 都已按各自锚点正确计算
    // ================================================================

    static void BuildHUD(GameObject parent, HUDUI hud)
    {
        // -- 提示文本：顶部居中，锚点 (0.5, 1) --
        hud.statusText = MakeText(parent, "StatusText", "选择档位 (1-4)", 24,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -35), new Vector2(700, 36), TextAlignmentOptions.Center);

        // -- 状态文本：右上角，锚点 (1, 1)，垂直堆叠 --
        float rowH = 34;
        hud.gearText      = MakeText(parent, "GearText",      "档位: 1",    18, TR(), TR(), new Vector2(-200, -80),              V2(220, 26));
        hud.heatText      = MakeText(parent, "HeatText",      "引擎: 12",   18, TR(), TR(), new Vector2(-200, -80 - rowH),     V2(280, 26));
        hud.lapText       = MakeText(parent, "LapText",       "圈数: 0/3",  18, TR(), TR(), new Vector2(-200, -80 - rowH*2),  V2(200, 26));
        hud.positionText  = MakeText(parent, "PositionText",  "位置: 0/--", 18, TR(), TR(), new Vector2(-200, -80 - rowH*3),  V2(200, 26));
        hud.aiStatusText  = MakeText(parent, "AIStatusText",  "AI: 就绪",   16, TR(), TR(), new Vector2(-200, -80 - rowH*4),  V2(260, 24));

        // -- 档位按钮：左上角，锚点 (0, 1) 水平排列 --
        BuildGearButtons(parent, hud);

        // -- 日志：左下角，锚点 (0, 0)，大区域 --
        hud.logText = MakeText(parent, "LogText", "", 14,
            BL(), BL(), new Vector2(20, 220), new Vector2(540, 200), TextAlignmentOptions.Left);

        // -- Reset 按钮：右下角，锚点 (1, 0) --
        hud.resetButton = MakeBtn(parent, "ResetBtn", "重新开始", BR(), BR(), new Vector2(-140, 50), V2(120, 44));
        if (hud.resetButton != null)
            hud.resetButton.GetComponent<Image>().color = new Color(0.9f, 0.75f, 0.2f);

        // -- 返回主菜单：比赛中始终可见，置于左上角独立顶栏，避免挤占档位操作区 --
        hud.returnToMenuButton = MakeBtn(parent, "ReturnToMenuBtn", "返回主菜单",
            TL(), TL(), new Vector2(24, -24), V2(160, 44));
        if (hud.returnToMenuButton != null)
            hud.returnToMenuButton.GetComponent<Image>().color = new Color(0.25f, 0.42f, 0.58f);

        // -- GameOver 面板：全屏居中，锚点 (0.5, 0.5) --
        var goPanel = NewGO("GameOverPanel", parent, typeof(Image));
        var gort = goPanel.GetComponent<RectTransform>();
        gort.anchorMin = gort.anchorMax = CC();
        gort.sizeDelta = new Vector2(560, 340);
        goPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        goPanel.SetActive(false);

        hud.gameOverPanel = goPanel;
        hud.gameOverText = MakeText(goPanel, "GameOverText", "=== 比赛结束 ===", 26,
            CC(), CC(), Vector2.zero, new Vector2(500, 240), TextAlignmentOptions.Center);

        // -- 返回主菜单 按钮（GameOver 面板内） --
        hud.backToMenuButton = MakeBtn(goPanel, "BackToMenuBtn", "返回主菜单",
            CC(), CC(), new Vector2(0, -110), V2(200, 50));
        if (hud.backToMenuButton != null)
            hud.backToMenuButton.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.9f);
    }

    static void BuildGearButtons(GameObject parent, HUDUI hud)
    {
        // 按钮容器：锚点左上角，pivot 对齐锚点
        var ct = NewGO("GearButtons", parent);
        var crt = ct.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = TL();   // (0, 1)
        crt.pivot = TL();                        // pivot 对齐，pos 直接表示左上角
        crt.anchoredPosition = new Vector2(24, -100);
        crt.sizeDelta = new Vector2(650, 50);

        float x = 0, w = 90, h = 44, gap = 12;

        hud.gear1Button        = MakeBtn(ct, "Gear1Btn",        "G1", TL(), TL(), new Vector2(x, 0), V2(w, h)); x += w + gap;
        hud.gear2Button        = MakeBtn(ct, "Gear2Btn",        "G2", TL(), TL(), new Vector2(x, 0), V2(w, h)); x += w + gap;
        hud.gear3Button        = MakeBtn(ct, "Gear3Btn",        "G3", TL(), TL(), new Vector2(x, 0), V2(w, h)); x += w + gap;
        hud.gear4Button        = MakeBtn(ct, "Gear4Btn",        "G4", TL(), TL(), new Vector2(x, 0), V2(w, h)); x += w + gap * 2;
        hud.confirmGearButton  = MakeBtn(ct, "ConfirmGearBtn",  "确认", TL(), TL(), new Vector2(x, 0), V2(120, h));

        if (hud.confirmGearButton != null)
            hud.confirmGearButton.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.9f);
    }

    static void BuildCardHand(GameObject parent, CardHandUI ch)
    {
        // -- 手牌容器：拉伸底部，紧贴底边 --
        var hc = NewGO("HandContainer", parent, typeof(HorizontalLayoutGroup));
        var hrt = hc.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.pivot = new Vector2(0.5f, 0);
        hrt.anchoredPosition = new Vector2(0, 40);
        hrt.sizeDelta = new Vector2(-40, 260);

        var hlg = hc.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;   // 不控制宽度 — CardHandUI 设 sizeDelta
        hlg.childControlHeight = false;  // 不控制高度
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ch.handContainer = hc.transform;

        // -- 加载卡牌精灵图 --
        ch.speedBgSprite = LoadSprite("Assets/Sprites/Cards/card_speed_bg.png");
        ch.heatBgSprite = LoadSprite("Assets/Sprites/Cards/card_heat_bg.png");
        ch.selectedOverlaySprite = LoadSprite("Assets/Sprites/Cards/card_selected_overlay.png");
        ch.heatIconSprite = LoadSprite("Assets/Sprites/Cards/card_heat_icon.png");
        ch.numberSprites = new Sprite[]
        {
            LoadSprite("Assets/Sprites/Cards/card_num_1.png"),
            LoadSprite("Assets/Sprites/Cards/card_num_2.png"),
            LoadSprite("Assets/Sprites/Cards/card_num_3.png"),
            LoadSprite("Assets/Sprites/Cards/card_num_4.png"),
        };

        // -- 牌堆信息：左下角 --
        ch.deckInfoText = MakeText(parent, "DeckInfo", "牌堆: 12速 + 3热", 15,
            BL(), BL(), new Vector2(20, 50), V2(280, 26), TextAlignmentOptions.Left);

        // -- 出牌 按钮：底栏靠右 --
        ch.playCardsButton = MakeBtn(parent, "PlayBtn", "出牌", BR(), BR(), new Vector2(-280, 40), V2(130, 50));
        if (ch.playCardsButton != null)
            ch.playCardsButton.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f);

        // -- CardPrefab 引用 --
        var cp = AssetDatabase.LoadAssetAtPath<GameObject>(CARD_PREFAB_PATH);
        if (cp != null) ch.cardPrefab = cp;
        else Debug.LogWarning("RaceCanvasBuilder: 未找到 " + CARD_PREFAB_PATH);
    }

    // ================================================================
    //  HELPERS — 所有坐标已对标锚点
    // ================================================================

    static TMP_Text MakeText(GameObject parent, string name, string txt, int size,
        Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 sizeDelta,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = NewGO(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = aMin;   // pivot 对齐锚点，anchoredPosition 直接 = 边角位置
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = txt;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.88f, 0.9f, 0.94f);

        // 显式设置字体，避免回退到 LiberationSans (Mobile shader → 模糊)
        var fontPath = AssetDatabase.GUIDToAssetPath(FONT_SDF_GUID);
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
        if (fontAsset != null) tmp.font = fontAsset;

        return tmp;
    }

    /// <summary>
    /// 加载 msyh SDF 字体，用于 MakeBtn 中的 Label。
    /// </summary>
    static TMP_FontAsset LoadDefaultFont()
    {
        var fontPath = AssetDatabase.GUIDToAssetPath(FONT_SDF_GUID);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
    }

    /// <summary>从 Assets 路径加载精灵图。文件不存在时返回 null（不报错）。</summary>
    static Sprite LoadSprite(string path)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
            Debug.LogWarning($"RaceCanvasBuilder: Sprite not found: {path} (will use color fallback)");
        return sp;
    }

    static Button MakeBtn(GameObject parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = NewGO(name, parent, typeof(Image), typeof(Button));
        ButtonClickAnimation.Attach(go.GetComponent<Button>());
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = aMin;   // pivot 对齐锚点
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.82f);

        // Label 子对象
        var lbl = NewGO("Label", go);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.StretchFull();
        var ltmp = lbl.AddComponent<TextMeshProUGUI>();
        ltmp.text = label;
        ltmp.fontSize = 18;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.color = new Color(0.06f, 0.08f, 0.14f);
        var font = LoadDefaultFont();
        if (font != null) ltmp.font = font;

        return go.GetComponent<Button>();
    }

    // ── 快捷锚点 ──
    static Vector2 TL()  => new Vector2(0, 1);     // top-left
    static Vector2 TR()  => new Vector2(1, 1);     // top-right
    static Vector2 BL()  => new Vector2(0, 0);     // bottom-left
    static Vector2 BR()  => new Vector2(1, 0);     // bottom-right
    static Vector2 CC()  => new Vector2(0.5f, 0.5f); // center
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static GameObject NewGO(string name, GameObject parent, params System.Type[] components)
    {
        var go = (components == null || components.Length == 0)
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name, components);
        if (parent != null)
        {
            go.transform.SetParent(parent.transform, false);
            // 确保有 RectTransform（如果 parent 有 Canvas）
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();
        }
        return go;
    }

    static void EnsureDir(string path)
    {
        string full = System.IO.Path.Combine(Application.dataPath, path["Assets/".Length..]);
        if (!System.IO.Directory.Exists(full))
            System.IO.Directory.CreateDirectory(full);
    }
}

// ── RectTransform 扩展 ──
public static class RectTransformExtensions
{
    public static void StretchFull(this RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
