using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 一键修复模糊文字问题。
/// 根因：LiberationSans 使用了 Mobile shader（无抗锯齿平滑），
/// msyh SDF 的 _GradientScale 与采样字号不匹配导致 SDF 边缘过宽。
/// 菜单: Foodular1 → Fix Blurry Text
/// </summary>
public static class FixBlurryText
{
    // ── 已知 GUID ──
    private const string LIBERATION_SANS_GUID  = "8f586378b4e144a9851e7b34d9b748ee";
    private const string MSYH_SDF_GUID         = "dc5fd1e0eb79f5e4b921552ceb9c30a8";
    private const string TMP_SDF_SHADER_GUID   = "68e6db2ebdc24f95958faec2be5558d6";   // TextMeshPro/Distance Field (标准桌面版)
    private const string TMP_SDF_MOBILE_GUID   = "fe393ace9b354375a9cb14cdbbc28be4";   // TextMeshPro/Mobile/Distance Field (移动版，无平滑)

    // ── 已知路径 ──
    private const string RACE_CANVAS_PATH = "Assets/Prefabs/UI/RaceCanvas.prefab";

    [MenuItem("Foodular1/Fix Blurry Text")]
    public static void Fix()
    {
        bool anyFix = false;

        anyFix |= FixLiberationSansShader();
        anyFix |= FixMsyhGradientScale();
        anyFix |= FixRaceCanvasPrefabFonts();
        anyFix |= FixTMPSettings();

        if (anyFix)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>✓ Blurry text fixes applied!</color> " +
                      "Enter Play Mode to verify. If still blurry, " +
                      "check Game view Scale = 1x and resolution = 1920×1080.");
        }
        else
        {
            Debug.Log("No fixes needed — everything looks correct.");
        }
    }

    // ──────────────────────────────────────────────
    //  Fix 1: LiberationSans 从 Mobile shader 切换到标准 shader
    // ──────────────────────────────────────────────
    static bool FixLiberationSansShader()
    {
        var mat = FindMaterial(LIBERATION_SANS_GUID);
        if (mat == null)
        {
            Debug.LogWarning("FixBlurryText: LiberationSans material not found.");
            return false;
        }

        var stdShader = FindShader(TMP_SDF_SHADER_GUID);
        if (stdShader == null)
        {
            Debug.LogWarning("FixBlurryText: Standard TMP_SDF shader not found.");
            return false;
        }

        if (mat.shader == stdShader)
        {
            Debug.Log("LiberationSans already uses standard shader — skipping.");
            return false;
        }

        mat.shader = stdShader;
        EditorUtility.SetDirty(mat);
        Debug.Log("<color=cyan>✓ LiberationSans:</color> Mobile → Standard TMP_SDF shader.");
        return true;
    }

    // ──────────────────────────────────────────────
    //  Fix 2: msyh SDF 的 _GradientScale 校准
    //  SDF 清晰度公式: GradientScale ≈ SamplingPointSize × 0.1
    //  当前: PointSize=237, GradientScale=65 → 比例 3.65 (正常应 ~24)
    //  GradientScale 太高 = SDF 抗锯齿边缘过宽 = 文字模糊
    // ──────────────────────────────────────────────
    static bool FixMsyhGradientScale()
    {
        var mat = FindMaterial(MSYH_SDF_GUID);
        if (mat == null)
        {
            Debug.LogWarning("FixBlurryText: msyh SDF material not found.");
            return false;
        }

        // 从 Font Asset 读取实际的采样字号
        var fontAssetPath = AssetDatabase.GUIDToAssetPath(MSYH_SDF_GUID);
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        float samplingPtSize = 90f;  // 兜底默认值
        if (fontAsset != null)
        {
            samplingPtSize = fontAsset.faceInfo.pointSize;
        }

        // TMP 标准公式: GradientScale = SamplingPointSize / 10 (向上取整)
        // 但实际需要配合 _ScaleRatioA/B/C 和 Canvas Scaler，此处按 0.1 比例校准
        float correctGS = Mathf.Round(samplingPtSize * 0.1f);
        float currentGS = mat.GetFloat("_GradientScale");

        if (Mathf.Abs(currentGS - correctGS) < 0.5f)
        {
            Debug.Log($"msyh GradientScale already correct ({currentGS}) — skipping.");
            return false;
        }

        mat.SetFloat("_GradientScale", correctGS);
        EditorUtility.SetDirty(mat);
        Debug.Log($"<color=cyan>✓ msyh SDF:</color> _GradientScale {currentGS} → {correctGS} " +
                  $"(samplingPtSize={samplingPtSize}).");
        return true;
    }

    // ──────────────────────────────────────────────
    //  Fix 3: RaceCanvas Prefab 中所有 TMP_Text 统一字体
    // ──────────────────────────────────────────────
    static bool FixRaceCanvasPrefabFonts()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RACE_CANVAS_PATH);
        if (prefab == null)
        {
            Debug.LogWarning("FixBlurryText: RaceCanvas.prefab not found.");
            return false;
        }

        var msyhFont = FindFontAsset(MSYH_SDF_GUID);
        if (msyhFont == null)
        {
            Debug.LogWarning("FixBlurryText: msyh SDF font asset not found.");
            return false;
        }

        bool changed = false;
        var tmpTexts = prefab.GetComponentsInChildren<TMP_Text>(true);
        foreach (var tmp in tmpTexts)
        {
            if (tmp.font != msyhFont)
            {
                tmp.font = msyhFont;
                EditorUtility.SetDirty(tmp);
                changed = true;
            }
        }

        if (changed)
        {
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"<color=cyan>✓ RaceCanvas:</color> {tmpTexts.Length} TMP texts → msyh SDF.");
        }
        else
        {
            Debug.Log("RaceCanvas already uses msyh for all texts — skipping.");
        }

        return changed;
    }

    // ──────────────────────────────────────────────
    //  Fix 4: TMP Settings 默认字体 + 回退链
    // ──────────────────────────────────────────────
    static bool FixTMPSettings()
    {
        var settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
        if (settings == null)
        {
            Debug.LogWarning("FixBlurryText: TMP Settings not found.");
            return false;
        }

        bool changed = false;
        var so = new SerializedObject(settings);

        // 默认字体 → msyh SDF（中文项目应该用中文字体作默认）
        var msyhFont = FindFontAsset(MSYH_SDF_GUID);
        var defaultFontProp = so.FindProperty("m_defaultFontAsset");
        if (msyhFont != null && defaultFontProp != null &&
            defaultFontProp.objectReferenceValue != msyhFont)
        {
            defaultFontProp.objectReferenceValue = msyhFont;
            changed = true;
            Debug.Log("<color=cyan>✓ TMP Settings:</color> Default font → msyh SDF.");
        }

        // 添加回退字体: LiberationSans（作为拉丁/数字的 fallback）
        var fallbackProp = so.FindProperty("m_fallbackFontAssets");
        var libFont = FindFontAsset(LIBERATION_SANS_GUID);
        if (libFont != null && fallbackProp != null)
        {
            bool hasFallback = false;
            for (int i = 0; i < fallbackProp.arraySize; i++)
            {
                if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == libFont)
                {
                    hasFallback = true;
                    break;
                }
            }
            if (!hasFallback)
            {
                fallbackProp.arraySize++;
                fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1)
                    .objectReferenceValue = libFont;
                changed = true;
                Debug.Log("<color=cyan>✓ TMP Settings:</color> Added LiberationSans as fallback.");
            }
        }

        if (changed) so.ApplyModifiedProperties();
        return changed;
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────
    static Material FindMaterial(string assetGuid)
    {
        var path = AssetDatabase.GUIDToAssetPath(assetGuid);
        if (string.IsNullOrEmpty(path)) return null;
        // Font Asset 文件本身包含 Material 定义；第一个 Material 子资源
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is Material mat) return mat;
        }
        return null;
    }

    static TMP_FontAsset FindFontAsset(string assetGuid)
    {
        var path = AssetDatabase.GUIDToAssetPath(assetGuid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    static Shader FindShader(string assetGuid)
    {
        var path = AssetDatabase.GUIDToAssetPath(assetGuid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<Shader>(path);
    }
}
