# WordFilterPlus Design

## Goal

Extend the vanilla distribution word filter (`Window.SaveData.IsFilterPass`) to support `@element` enchantment matching, `rarity:x` comparisons, and `identified:true/false` predicates. The patch covers all four call sites in the base game, meaning it affects manual auto-dump, AI worker hauling (delegated farming), container search destination resolution, and on-walk auto-pick.

## Background

### The distribution filter system

Containers in Elin can be configured with an `AutodumpFlag` and an optional `filter` string (stored in `Window.SaveData`). When auto-dump or an AI worker decides where to deposit an item, it calls `Window.SaveData.IsFilterPass(string text)`, where `text` is the item's full display name.

`BuildFilter()` parses the comma-separated filter string with prefix rules:
- No prefix → include if name contains this word
- `-` prefix → exclude if name contains this word  
- `+` prefix → pass unconditionally if name contains this word

`IsFilterPass` then does plain `string.Contains` against the display name. It has no awareness of item properties like enchantments, rarity, or identified state.

### The four call sites (all must be covered)

| File | Context |
|---|---|
| `TaskDump.ListThingsToPut` (×3) | Manual dump action — all three `AutodumpFlag` modes |
| `Zone.TryAddThingInSharedContainer` | AI workers depositing items — delegated farming |
| `ThingContainer.TrySearchContainer` | Finding destination container during haul |
| `Chara.cs` ~3121 | On-walk auto-pick filter |

All call sites follow the same pattern: `data.IsFilterPass(t.GetName(NameStyle.Full, 1))` where `t` is a `Thing` in scope.

### The `@operator` element search

`Thing.MatchEncSearch(string s)` in the base game searches an item's elements (enchantments, skills, attributes) by name substring. This is what Ctrl+F's `@swimming` syntax uses — the `@` triggers enchantment/element search rather than name search. We borrow this exact logic for the distribution filter. The term `@operator` in this doc refers to any such `@`-prefixed token.

---

## Filter syntax added by this plugin

All new tokens are composable with existing plain-name tokens and obey the same `-`/`+` prefix rules.

| Token | Example | Meaning |
|---|---|---|
| `@operator` | `@swimming` | Item has an element (enchantment/skill/attribute) whose name contains "swimming" — the same search used by Ctrl+F's `@` prefix |
| `-@operator` | `-@fire` | Exclude items with that element |
| `rarity:=name` | `rarity:=legendary` | Item rarity exactly equals that level |
| `rarity:!=name` | `rarity:!=normal` | Item rarity is not that level |
| `rarity:>name` | `rarity:>normal` | Item rarity strictly above that level |
| `rarity:>=name` | `rarity:>=superior` | Item rarity at least that level |
| `rarity:<name` | `rarity:<legendary` | Item rarity strictly below that level |
| `rarity:<=name` | `rarity:<=legendary` | Item rarity at most that level |
| `identified:true` | `identified:true` | Only identified items |
| `identified:false` | `identified:false` | Only unidentified items |

Shorthand: bare `rarity:name` (no operator) is equivalent to `rarity:=name`.

Valid rarity names (case-insensitive): `crude`, `normal`, `superior`, `legendary`, `mythical`, `artifact`.

Mixed example: `sword,@fire,-shield,rarity:>=superior`

Plain name tokens continue to work exactly as before.

---

## Architecture

### Core mechanism: `[ThreadStatic]` filter context

`IsFilterPass` only receives a `string`, not a `Thing`. To evaluate extended tokens we need the `Thing`. The solution is a static thread-local slot:

```csharp
// Filter/FilterContext.cs
internal static class FilterContext
{
    [ThreadStatic]
    internal static Thing? Current;
}
```

Each of the 4 call sites gets a Harmony **prefix** (sets `FilterContext.Current = t`) and **finalizer** (clears it). The window between set and clear is the single `IsFilterPass` call, so there is no risk of the context leaking.

### Patch on `IsFilterPass`

A Harmony **prefix** on `Window.SaveData.IsFilterPass` takes full ownership when the filter string contains any extended token. It:

1. Scans `filterStrs` for tokens starting with `@` (`@operator`), or strings starting with `rarity:` or `identified:`.
2. If no extended tokens are present, returns `true` (run original — zero overhead for unaugmented chests).
3. If extended tokens are present, evaluates the full filter (plain + extended) against `FilterContext.Current` and `text`, sets `__result`, returns `false` (skip original).

Graceful degradation: if `FilterContext.Current` is null when an extended token is encountered, that token evaluates as non-matching (same behaviour as an item with no elements). The filter continues evaluating other tokens.

---

## File structure

```
Elin.SearchPlugin/
  WordFilterPlus.sln
  WordFilterPlus/
    WordFilterPlus.csproj         — netstandard2.0, references ElinGamePath
    Directory.Build.props         — BepInEx/Harmony refs, ElinGamePath default
    AsmInfo.cs                    — AssemblyVersion from ModInfo.Version
    Plugin.cs                     — BepInEx entry; Harmony.CreateAndPatchAll
    Filter/
      FilterContext.cs            — [ThreadStatic] Thing Current; Set/Clear helpers
      ExtendedFilter.cs           — Token parsing; EvaluateFilter(); rarity/element/identified logic
    Patches/
      CallSitePatches.cs          — Prefix+finalizer on all 4 call sites; sets FilterContext.Current
      IsFilterPassPatch.cs        — Prefix on Window.SaveData.IsFilterPass; full evaluation
  .tools/
    invoke_serena_project_index.ps1
    reindex_serena.ps1
    serena_output_filter.ps1
  .vscode/
    tasks.json                    — "Serena: Reindex WordFilterPlus" task
  reindex_serena.bat
```

---

## ModInfo

| Field | Value |
|---|---|
| Name | `WordFilterPlus` |
| GUID | `dk.elinplugins.wordfilterplus` |
| Version | `1.0.0` |

---

## Key design decisions

- **Full replacement in prefix** — `IsFilterPassPatch` handles all tokens when extended tokens are present, setting `__result` and returning `false`. No interaction with original method logic.
- **Zero overhead for plain filters** — chests using only plain name tokens hit the early-exit path and run the unmodified original.
- **`BuildFilter` is not patched** — `@operator` tokens pass through `BuildFilter` unmodified (not a recognised prefix char). `-@operator` is parsed as option=1 (block) with text `@operator`, which the evaluator detects correctly. `rarity:` and `identified:` tokens are also passed through unchanged.
- **No save-data changes** — filter strings are user-entered text; extended tokens are stored as-is in the existing `filter` field.
- **Thread safety** — `[ThreadStatic]` ensures correctness if the game ever calls these paths from multiple threads; no locking needed.

---

## What this does NOT change

- `WidgetSearch` (Ctrl+F) — left untouched; it already has its own `@` element search via `MatchEncSearch`.
- `BuildFilter` parsing logic — extended tokens are parsed at evaluation time by `ExtendedFilter`, not during `BuildFilter`.
- Any CWL stock distribution logic — this plugin operates on the base game system only.

---

## Resolved decisions

- **`@operator` on unidentified items** — returns false (mirrors vanilla `MatchEncSearch` / Ctrl+F behaviour). An unidentified item's elements are not revealed, so it cannot match an element filter.
