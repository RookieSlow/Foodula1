using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor 工具：一键生成 MainMenu 场景 + 重命名 SampleScene → Race + 更新 Build Settings。
/// 菜单: Foodula1 → Build MainMenu + Setup Scenes
///
/// 生成后 MainMenuCanvas 挂载 MainMenuUI，按钮回调在 MainMenuUI.Start() 中绑定。
/// </summary>
public static class MainMenuBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";
    private const string FONT_SDF_GUID = "5358f61b11e22f34e9fe8942033cf67b"; // SourceHanSansTC-Medium-HQ

    [MenuItem("Tools/Build MainMenu + Setup Scenes")]
    public static void Build()
    {
        // ── 创建新场景（含 Main Camera + Directional Light） ──
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ── 配置相机 ──
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = new Color(0.051f, 0.067f, 0.09f); // #0D1117
        }

        // 不需要 Directional Light — UI 场景靠 Canvas 自发光
        var light = Object.FindObjectOfType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        // ── EventSystem（UI 按钮点击必需，DefaultGameObjects 不含它） ──
        GameObject esGO = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // ── Canvas Root ──
        GameObject canvasGO = NewGO("MainMenuCanvas", null,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MainMenuUI));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildMenu(canvasGO);

        // ── 保存场景 ──
        EnsureDir("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, SCENE_PATH);

        // ── 重命名 SampleScene → Race ──
        RenameSampleScene();

        // ── 更新 Build Settings ──
        SetupBuildSettings();

        Debug.Log("<color=green>✓ 主菜单场景已生成！</color>\n"
            + "Build Settings: MainMenu (0) → Race (1)\n"
            + "按钮回调在 MainMenuUI.Start() 中自动绑定。");
    }

    // ================================================================
    //  MENU LAYOUT  (1920×1080 基准，所有坐标相对锚点)
    // ================================================================

    static void BuildMenu(GameObject canvasGO)
    {
        var menuUI = canvasGO.GetComponent<MainMenuUI>();

        // ── 标题 ──
        MakeText(canvasGO, "TitleText", "Foodula1", 80,
            CC(), CC(), new Vector2(0, 200), new Vector2(700, 100),
            TextAlignmentOptions.Center, FontStyles.Bold, Color.white);

        // ── 副标题 ──
        MakeText(canvasGO, "SubtitleText", "── 美食卡牌赛车 ──", 24,
            CC(), CC(), new Vector2(0, 130), new Vector2(400, 36),
            TextAlignmentOptions.Center, FontStyles.Normal,
            new Color(0.345f, 0.65f, 1f)); // #58A6FF

        // ── 自由赛事 / 生涯模式 ──
        menuUI.startRaceButton = MakeMenuBtn(canvasGO, "StartRaceBtn", MainMenuLabels.QuickRace,
            new Vector2(-170, 20), new Color(0.18f, 0.72f, 0.22f))
            .GetComponent<Button>();

        MakeMenuBtn(canvasGO, "CareerBtn", MainMenuLabels.Career,
            new Vector2(170, 20), new Color(0.5f, 0.25f, 0.62f));

        // ── 车手选择按钮 ──
        MakeMenuBtn(canvasGO, "GarageBtn", "车手选择",
            new Vector2(170, -70), new Color(0.18f, 0.42f, 0.62f));

        // ── 车队科技树按钮 ──
        MakeMenuBtn(canvasGO, "TechTreeBtn", "车队科技树",
            new Vector2(-170, -70), new Color(0.42f, 0.28f, 0.14f));

        // ── 新手教程按钮 ──
        MakeMenuBtn(canvasGO, "TutorialBtn", "新手教程",
            new Vector2(-170, -160), new Color(0.16f, 0.47f, 0.56f));

        // ── 设置按钮 ──
        MakeMenuBtn(canvasGO, "SettingsBtn", "设置",
            new Vector2(170, -160), new Color(0.28f, 0.34f, 0.48f));

        // ── 退出游戏 按钮 ──
        menuUI.quitButton = MakeMenuBtn(canvasGO, "QuitBtn", "退出游戏",
            new Vector2(0, -250), new Color(0.5f, 0.15f, 0.15f))
            .GetComponent<Button>();

        // ── 版本号（底部居中） ──
        MakeText(canvasGO, "VersionText", "v0.1 Demo", 14,
            BC(), BC(), new Vector2(0, 40), new Vector2(200, 24),
            TextAlignmentOptions.Center, FontStyles.Normal,
            new Color(0.4f, 0.4f, 0.4f));
    }

    // ================================================================
    //  HELPERS
    // ================================================================

    static TMP_Text MakeText(GameObject parent, string name, string txt, int size,
        Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 sizeDelta,
        TextAlignmentOptions align, FontStyles style, Color color)
    {
        var go = NewGO(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = aMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = txt;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.color = color;

        var fontAsset = LoadFont();
        if (fontAsset != null) tmp.font = fontAsset;

        return tmp;
    }

    /// <summary>创建菜单按钮 — 带 Image + Button + TMP Label。</summary>
    static GameObject MakeMenuBtn(GameObject parent, string name, string label,
        Vector2 anchoredPos, Color bgColor)
    {
        var go = NewGO(name, parent, typeof(Image), typeof(Button));
        ButtonClickAnimation.Attach(go.GetComponent<Button>());
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = CC();
        rt.pivot = CC();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(320, 64);
        go.GetComponent<Image>().color = bgColor;

        // Label
        var lbl = NewGO("Label", go);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var ltmp = lbl.AddComponent<TextMeshProUGUI>();
        ltmp.text = label;
        ltmp.fontSize = 28;
        ltmp.alignment = TextAlignmentOptions.Center;
        ltmp.color = Color.white;
        var font = LoadFont();
        if (font != null) ltmp.font = font;

        return go;
    }

    static TMP_FontAsset LoadFont()
    {
        var fontPath = AssetDatabase.GUIDToAssetPath(FONT_SDF_GUID);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
    }

    static GameObject NewGO(string name, GameObject parent, params System.Type[] components)
    {
        var go = (components == null || components.Length == 0)
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name, components);
        if (parent != null)
            go.transform.SetParent(parent.transform, false);
        if (go.GetComponent<RectTransform>() == null)
            go.AddComponent<RectTransform>();
        return go;
    }

    // ── 锚点快捷 ──
    static Vector2 CC() => new Vector2(0.5f, 0.5f);   // center
    static Vector2 BC() => new Vector2(0.5f, 0);       // bottom-center

    static void EnsureDir(string path)
    {
        string full = System.IO.Path.Combine(Application.dataPath, path["Assets/".Length..]);
        if (!System.IO.Directory.Exists(full))
            System.IO.Directory.CreateDirectory(full);
    }

    // ================================================================
    //  场景重命名 & Build Settings
    // ================================================================

    static void RenameSampleScene()
    {
        string oldPath = "Assets/Scenes/SampleScene.unity";
        if (AssetDatabase.LoadAssetAtPath<Object>(oldPath) != null)
        {
            string newPath = "Assets/Scenes/Race.unity";
            // 如果 Race.unity 已存在（重复运行），先删旧 SampleScene 的 .meta
            if (AssetDatabase.LoadAssetAtPath<Object>(newPath) != null)
            {
                Debug.LogWarning("Race.unity 已存在，跳过重命名。");
                return;
            }
            string result = AssetDatabase.MoveAsset(oldPath, newPath);
            if (string.IsNullOrEmpty(result))
                Debug.Log("SampleScene.unity → Race.unity 重命名完成。");
            else
                Debug.LogWarning("场景重命名失败: " + result);
        }
        else
        {
            Debug.LogWarning("SampleScene.unity 不存在，跳过重命名。");
        }
    }

    static void SetupBuildSettings()
    {
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Race.unity", true),
        };
    }
}
