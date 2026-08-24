using UnityEditor;
using UnityEngine;

/// <summary>
/// Prefab-safe authoring entry point for the race HUD. The default layout is
/// only rebuilt after an explicit button press because rebuilding resets the
/// authored RectTransform positions.
/// </summary>
public sealed class RaceUILayoutAuthoringWindow : EditorWindow
{
    private const string PrefabPath = "Assets/Prefabs/UI/RaceCanvas.prefab";

    [MenuItem("Tools/Foodula1/Race UI Authoring")]
    public static void Open()
    {
        GetWindow<RaceUILayoutAuthoringWindow>("Race UI Authoring");
    }

    [MenuItem("Tools/Foodula1/Bake Default Race UI Layout")]
    public static void BakeDefaultLayoutIntoPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Race UI layout cannot be baked while entering or running Play Mode.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            HUDUI hud = prefabRoot.GetComponentInChildren<HUDUI>(true);
            CardHandUI cardHand = prefabRoot.GetComponentInChildren<CardHandUI>(true);
            if (hud == null || cardHand == null)
            {
                Debug.LogError("RaceCanvas prefab must contain both HUDUI and CardHandUI before layout can be baked.");
                return;
            }

            RaceUILayoutController layout = prefabRoot.GetComponent<RaceUILayoutController>();
            if (layout == null)
                layout = prefabRoot.AddComponent<RaceUILayoutController>();

            layout.RebuildDefaultLayout(hud, cardHand);
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("RaceCanvas authored layout baked successfully: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("RaceCanvas 所见即所得布局", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "可以直接选择场景中的 RaceCanvas，或打开 Prefab 后调整四个主面板及其控件。" +
            "游戏运行时会保留这些 RectTransform。场景实例上的修改可通过 Overrides 应用回 Prefab。\n\n" +
            "“写入默认布局”会把位置重置为项目当前默认值，仅在需要恢复或升级旧 Prefab 时使用。",
            MessageType.Info);

        if (GUILayout.Button("选中当前场景的 RaceCanvas", GUILayout.Height(30f)))
        {
            GameObject sceneCanvas = GameObject.Find("RaceCanvas");
            if (sceneCanvas != null)
            {
                Selection.activeGameObject = sceneCanvas;
                EditorGUIUtility.PingObject(sceneCanvas);
            }
            else
            {
                Debug.LogWarning("Current scene does not contain an active RaceCanvas.");
            }
        }

        if (GUILayout.Button("打开 RaceCanvas Prefab", GUILayout.Height(30f)))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
                AssetDatabase.OpenAsset(prefab);
        }

        EditorGUILayout.Space(8f);
        GUI.backgroundColor = new Color(1f, 0.78f, 0.38f);
        if (GUILayout.Button("将默认布局写入 RaceCanvas Prefab", GUILayout.Height(34f)))
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "写入默认 Race UI 布局",
                "这会重置 RaceCanvas 中主面板和控件的位置。是否继续？",
                "继续",
                "取消");
            if (confirmed)
                BakeDefaultLayoutIntoPrefab();
        }
        GUI.backgroundColor = Color.white;
    }
}
