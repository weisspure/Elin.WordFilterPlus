using System;

namespace WordFilterPlus.Filter;

internal static class ExtendedFilter
{
    private const int OptionInclude = 0;
    private const int OptionBlock   = 1;
    private const int OptionPass    = 2;

    /// <summary>
    /// Returns true if any token in the already-built filter strings is an extended token.
    /// Called in the IsFilterPass prefix to decide whether to take ownership.
    /// </summary>
    internal static bool HasExtendedTokens(string[] filterStrs)
    {
        foreach (var s in filterStrs)
        {
            if (s.Length > 1 && s[0] == '@') return true;
            if (s.StartsWith("rarity:", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Full filter evaluation replacing IsFilterPass when extended tokens are present.
    /// Replicates the plain-token logic of the original and adds extended token evaluation.
    /// </summary>
    internal static global::Window.SaveData.FilterResult EvaluateFilter(
        string[] filterStrs, int[] filterOptions, string nameText, Thing? thing)
    {
        bool hasExtended = HasExtendedTokens(filterStrs);
        bool anyInclude      = false;
        bool hasIncludeToken = false;

        for (int i = 0; i < filterStrs.Length; i++)
        {
            var token  = filterStrs[i];
            var option = filterOptions[i];
            if (token.Length == 0) continue;

            bool matched = EvaluateToken(token, nameText, thing);

            switch (option)
            {
                case OptionBlock:
                    if (matched) return global::Window.SaveData.FilterResult.Block;
                    break;
                case OptionPass:
                    if (matched) return global::Window.SaveData.FilterResult.PassWithoutFurtherTest;
                    break;
                case OptionInclude:
                    hasIncludeToken = true;
                    if (matched) anyInclude = true;
                    break;
            }
        }

        return (!anyInclude && hasIncludeToken) 
            ? global::Window.SaveData.FilterResult.Block 
            : global::Window.SaveData.FilterResult.Pass;
    }

    private static bool EvaluateToken(string token, string nameText, Thing? thing)
    {
        // @ — enchantment/property search
        if (token.Length > 1 && token[0] == '@')
        {
            var query = token.Substring(1).ToLower();
            if (query == "identified") return thing != null && thing.IsIdentified;
            return thing != null && MatchElements(thing, query);
        }

        // rarity: comparisons
        if (token.StartsWith("rarity:", StringComparison.OrdinalIgnoreCase))
        {
            var expr = token.Substring(7);
            return thing != null && MatchRarity(expr, thing.rarity);
        }

        // Plain name token
        return nameText.Contains(token);
    }

    /// <summary>
    /// Mirrors Thing.MatchEncSearch: unidentified items return false.
    /// </summary>
    private static bool MatchElements(Thing thing, string query)
    {
        if (!thing.IsIdentified) return false;
        foreach (var el in thing.elements.dict.Values)
        {
            if (el.Value == 0) continue;
            if (el.source.name.ToLower().Contains(query)) return true;
            if (el.source.GetName().ToLower().Contains(query)) return true;
        }
        return false;
    }

    /// <summary>
    /// Parses ">=superior", "legendary", "!=mythical" etc. and compares against itemRarity.
    /// Bare name (no operator prefix) is treated as "=".
    /// Accepts both internal names (superior, legendary) and display names (good, miracle).
    /// </summary>
    private static bool MatchRarity(string expr, Rarity itemRarity)
    {
        string op;
        string name;

        if (expr.StartsWith(">="))      { op = ">="; name = expr.Substring(2); }
        else if (expr.StartsWith("<=")) { op = "<="; name = expr.Substring(2); }
        else if (expr.StartsWith(">"))  { op = ">";  name = expr.Substring(1); }
        else if (expr.StartsWith("<"))  { op = "<";  name = expr.Substring(1); }
        else if (expr.StartsWith("="))  { op = "=";  name = expr.Substring(1); }
        else                            { op = "=";  name = expr; }

        if (!TryParseRarity(name, out Rarity target)) return false;

        return op switch
        {
            "="  => itemRarity == target,
            ">"  => itemRarity >  target,
            ">=" => itemRarity >= target,
            "<"  => itemRarity <  target,
            "<=" => itemRarity <= target,
            _    => false,
        };
    }

    /// <summary>
    /// Parse both internal names (superior, legendary, etc.) and display names (good, miracle, godly, etc.)
    /// </summary>
    private static bool TryParseRarity(string name, out Rarity rarity)
    {
        name = name.Trim();

        // Try direct enum parse (internal names: crude, normal, superior, legendary, mythical, artifact)
        if (Enum.TryParse(name, ignoreCase: true, out rarity))
            return true;

        // Map display names to internal rarity
        rarity = name.ToLower() switch
        {
            "good" => Rarity.Superior,
            "miracle" => Rarity.Legendary,
            "godly" => Rarity.Mythical,
            "artefact" or "precious" => Rarity.Artifact,
            _ => Rarity.Normal
        };

        // Return true only if it was a recognized display name
        return name.ToLower() is "good" or "miracle" or "godly" or "artefact" or "precious";
    }
}
