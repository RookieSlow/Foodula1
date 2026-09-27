using System;
using System.Text;

/// <summary>Pure rich-text formatting for the fixed, scrollable race result panel.</summary>
public static class RaceResultPresentationRules
{
    public static string FormatForRichText(string result)
    {
        if (string.IsNullOrEmpty(result))
            return string.Empty;

        string[] lines = result.Replace("\r\n", "\n").Split('\n');
        bool insideStandings = false;
        bool sawRank = false;
        var formatted = new StringBuilder(result.Length + 128);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Contains("=== 比赛结果 ==="))
            {
                insideStandings = true;
                continue;
            }

            if (insideStandings && TryReadRank(line, out int rank))
            {
                sawRank = true;
                line = FormatRankLine(rank, line);
            }
            else if (insideStandings && sawRank && string.IsNullOrWhiteSpace(line))
            {
                insideStandings = false;
            }

            if (formatted.Length > 0)
                formatted.Append('\n');
            formatted.Append(line);
        }

        return formatted.ToString().TrimStart('\n');
    }

    private static bool TryReadRank(string line, out int rank)
    {
        rank = 0;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        int separator = line.IndexOf('.');
        return separator > 0 && separator <= 2 &&
            int.TryParse(line.Substring(0, separator), out rank) && rank > 0;
    }

    private static string FormatRankLine(int rank, string line)
    {
        switch (rank)
        {
            case 1:
                return $"<color=#FFD75A><b>【冠军】 {line}</b></color>";
            case 2:
                return $"<color=#DDE7F2><b>【亚军】 {line}</b></color>";
            case 3:
                return $"<color=#E7A56A><b>【季军】 {line}</b></color>";
            default:
                return $"<color=#C2CDDD>{line}</color>";
        }
    }
}
