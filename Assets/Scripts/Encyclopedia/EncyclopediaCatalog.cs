using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A versioned, data-driven encyclopedia shipped from Resources.</summary>
[Serializable]
public sealed class EncyclopediaCatalogData
{
    public int version;
    public EncyclopediaEntry[] entries;
}

/// <summary>One stable encyclopedia topic. relatedRuleIds trace content back to runtime catalogs.</summary>
[Serializable]
public sealed class EncyclopediaEntry
{
    public string id;
    public string category;
    public string title;
    public string summary;
    public string body;
    public string[] keywords;
    public string[] relatedRuleIds;
}

public static class EncyclopediaCatalog
{
    public const int CurrentVersion = 1;
    public const string ResourcePath = "Configs/encyclopedia_zh";

    public static readonly string[] RequiredTopicIds =
    {
        "turn-flow",
        "cards-speed",
        "cards-heat",
        "cards-trick",
        "gear",
        "deck-zones",
        "heat-cooling",
        "missing-card",
        "corners-spin",
        "slipstream",
        "weather",
        "pit-lane",
        "teams-vehicles",
        "special-cards",
        "drivers",
        "tech-tree",
        "ui-terms"
    };

    public static EncyclopediaCatalogData LoadDefault()
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        return asset == null ? null : FromJson(asset.text);
    }

    public static EncyclopediaCatalogData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonUtility.FromJson<EncyclopediaCatalogData>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static EncyclopediaEntry FindById(EncyclopediaCatalogData data, string id)
    {
        if (data?.entries == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < data.entries.Length; i++)
        {
            EncyclopediaEntry entry = data.entries[i];
            if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    /// <summary>Returns every authoring error so CI can report the whole catalog in one pass.</summary>
    public static List<string> Validate(EncyclopediaCatalogData data)
    {
        var errors = new List<string>();
        if (data == null)
        {
            errors.Add("Catalog could not be loaded.");
            return errors;
        }

        if (data.version != CurrentVersion)
            errors.Add($"Unsupported catalog version {data.version}.");
        if (data.entries == null || data.entries.Length == 0)
        {
            errors.Add("Catalog has no entries.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < data.entries.Length; i++)
        {
            EncyclopediaEntry entry = data.entries[i];
            if (entry == null)
            {
                errors.Add($"Entry {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.id)) errors.Add($"Entry {i} has no id.");
            else if (!ids.Add(entry.id)) errors.Add($"Duplicate entry id: {entry.id}.");
            if (string.IsNullOrWhiteSpace(entry.category)) errors.Add($"Entry {entry.id} has no category.");
            if (string.IsNullOrWhiteSpace(entry.title)) errors.Add($"Entry {entry.id} has no title.");
            if (string.IsNullOrWhiteSpace(entry.summary)) errors.Add($"Entry {entry.id} has no summary.");
            if (string.IsNullOrWhiteSpace(entry.body)) errors.Add($"Entry {entry.id} has no body.");
        }

        for (int i = 0; i < RequiredTopicIds.Length; i++)
            if (!ids.Contains(RequiredTopicIds[i])) errors.Add($"Missing required topic: {RequiredTopicIds[i]}.");

        return errors;
    }
}
