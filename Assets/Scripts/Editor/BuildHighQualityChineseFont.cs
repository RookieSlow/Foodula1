using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the runtime Chinese font used by every UI canvas.
///
/// The old static CJK atlas was sampled at 21pt, which is too small once a
/// 1920x1080 canvas is scaled to a smaller window.  The other msyh asset in
/// the project was sampled at 237pt, but its atlas was imported as unreadable
/// and therefore could not add Chinese glyphs at runtime.  This asset keeps a
/// readable dynamic atlas and enables multi-atlas growth for crisp text at
/// every supported resolution.
/// </summary>
public static class BuildHighQualityChineseFont
{
    private const string SOURCE_FONT_PATH =
        "Assets/TmpFont/Fonts/Chinese/SourceHanSansTC-Medium.otf";
    private const string OUTPUT_FONT_PATH =
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset";
    private const string LEGACY_FONT_PATH =
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/思源黑體-Medium.asset";
    private const string TMP_SDF_SHADER_GUID =
        "68e6db2ebdc24f95958faec2be5558d6";

    // Text already visible before the first dynamic frame.  Further strings
    // are added automatically by TMP as they are used at runtime.
    private const string SEED_CHARACTERS =
        "食品卡牌赛车开始比赛车队科技树车手退出游戏选择赛道确认返回下一步取消" +
        "速度档位换挡内道外道直道弯道弯心限速圈数回合手牌弃牌堆结束" +
        "中国队银石赛道勒芒纽伯格林印地赛道玩家电脑当前成绩排名";

    [MenuItem("Tools/Build High Quality Chinese Font")]
    public static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SOURCE_FONT_PATH);
        if (source == null)
        {
            Debug.LogError($"BuildHighQualityChineseFont: source font not found at {SOURCE_FONT_PATH}.");
            return;
        }

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OUTPUT_FONT_PATH);
        if (fontAsset != null && (fontAsset.material == null || fontAsset.atlasTexture == null))
        {
            // A previous interrupted import may have saved only the main
            // MonoBehaviour. Rebuild it so its material and atlas are proper
            // sub-assets instead of fileID 0 references.
            AssetDatabase.DeleteAsset(OUTPUT_FONT_PATH);
            fontAsset = null;
        }

        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                64,
                8,
                GlyphRenderMode.SDFAA,
                4096,
                4096,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("BuildHighQualityChineseFont: TMP_FontAsset.CreateFontAsset returned null.");
                return;
            }

            fontAsset.name = "SourceHanSansTC-Medium-HQ";
            AssetDatabase.CreateAsset(fontAsset, OUTPUT_FONT_PATH);

            // CreateFontAsset returns the material and atlas as in-memory
            // objects. They must be explicitly embedded in the .asset file;
            // otherwise Unity serializes the references as fileID 0.
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "SourceHanSansTC-Medium-HQ Material";
                fontAsset.material.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(fontAsset.material, OUTPUT_FONT_PATH);
            }

            foreach (var atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture == null) continue;
                atlasTexture.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(atlasTexture, OUTPUT_FONT_PATH);
            }
        }

        fontAsset.isMultiAtlasTexturesEnabled = true;
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        var standardSdfPath = AssetDatabase.GUIDToAssetPath(TMP_SDF_SHADER_GUID);
        var standardSdf = AssetDatabase.LoadAssetAtPath<Shader>(standardSdfPath);
        if (fontAsset.material != null && standardSdf != null)
        {
            // CreateFontAsset defaults to the mobile distance-field shader.
            // The desktop SDF shader keeps its smooth edge filtering at small
            // CanvasScaler factors and avoids the blocky/soft mobile variant.
            fontAsset.material.shader = standardSdf;
        }

        var legacyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LEGACY_FONT_PATH);
        if (legacyFont != null)
        {
            var fallbacks = fontAsset.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            if (!fallbacks.Contains(legacyFont)) fallbacks.Add(legacyFont);
            fontAsset.fallbackFontAssetTable = fallbacks;
        }

        // Populate the most common UI glyphs while the atlas is readable in
        // the editor. Missing strings can still be added by TMP at runtime.
        fontAsset.TryAddCharacters(SEED_CHARACTERS, out _, false);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var atlas = fontAsset.atlasTexture;
        var readable = atlas != null && atlas.isReadable;
        Debug.Log($"<color=green>High quality Chinese font ready:</color> {OUTPUT_FONT_PATH} " +
                  $"(sampling={fontAsset.faceInfo.pointSize:0}, atlas={fontAsset.atlasWidth}x{fontAsset.atlasHeight}, " +
                  $"readable={readable}, atlases={fontAsset.atlasTextureCount}).");
    }
}
