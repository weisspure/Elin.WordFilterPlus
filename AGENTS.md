# AGENTS.md — WordFilterPlus

Context and hard-earned lessons for AI agents working on this repo.

---

## Architecture

**BepInEx 5.x + Harmony 2.x** plugin for the game Elin (Unity, net472).

The plugin intercepts `Window.SaveData.IsFilterPass(string text)` to add extended filter token support. The vanilla method only does `text.Contains()` — it has zero awareness of `@`, rarity, or any property. That context is critical.

### Execution flow

```
Thing.GetName(NameStyle, int)        ← CallSitePatches.Prefix sets FilterContext.Current = __instance
  → Window.SaveData.IsFilterPass()  ← IsFilterPassPatch.Prefix: if extended tokens, evaluate and skip original
                                    ← IsFilterPassPatch.Postfix: always clears FilterContext.Current = null
```

**`FilterContext.Current` is `[ThreadStatic]`** — never stored as a field on the patch class. The attribute is on the static field in `FilterContext.cs`.

### Why `Thing.GetName` is the capture site

`IsFilterPass` receives only a `string text` (the item's display name). To access the `Thing` object (for rarity, identification, elements), we capture it in `Thing.GetName` which is called immediately before. This is the only reliable injection point — do not move it.

---

## Vanilla operator handling — important gotcha

The game's `BuildFilter()` pre-processes the raw filter string **before** `IsFilterPass` is ever called:
- `-token` → `filterOptions[i] = 1` (Block), token stripped of `-`
- `+token` → `filterOptions[i] = 2` (PassWithoutFurtherTest), token stripped of `+`
- bare token → `filterOptions[i] = 0` (Include)

By the time our prefix runs, `filterStrs[i]` is already stripped. We respect `filterOptions` the same way the original does — this is why `-@fire` and `-rarity:>=good` work for free.

---

## Rarity names

The `Rarity` enum (from `Elin/Rarity.cs`):

```csharp
public enum Rarity { Random=-999, Crude=-1, Normal=0, Superior=1, Legendary=2, Mythical=3, Artifact=4 }
```

The **internal names** (enum names) differ from the **display names** shown in-game. `TryParseRarity` accepts both:

| Internal name | Display name alias |
|---------------|--------------------|
| crude         | (none)             |
| normal        | (none)             |
| superior      | good               |
| legendary     | miracle            |
| mythical      | godly              |
| artifact      | artefact, precious |

`Enum.TryParse` handles internal names; the switch handles display aliases. Do not remove either branch.

---

## Syntax decisions (do not revert)

- `@<query>` for boolean/search properties: `@fire`, `@identified`
- `rarity:` for comparison: `rarity:>=good`, `rarity:legendary`
- **`!=` was intentionally removed** — use `-rarity:=normal` instead
- **`identified:true/false` was intentionally removed** — use `@identified` / `-@identified`
- Both internal and display rarity names are accepted (feature, not accident)

---

## Target framework

**net472** — required for Workshop/PackageChainloader (FixedPackageLoader) compatibility. Do not change to net6+ or netstandard2.1. The decompiled game code also targets net472.

---

## File layout

```
WordFilterPlus/
  Plugin.cs                    — BepInEx entry point, Harmony.CreateAndPatchAll
  Filter/
    ExtendedFilter.cs          — all filter evaluation logic
    FilterContext.cs           — [ThreadStatic] Current Thing reference
  Patches/
    IsFilterPassPatch.cs       — Harmony prefix/postfix on IsFilterPass
    CallSitePatches.cs         — Harmony prefix on Thing.GetName to capture Thing
  package/
    package.xml                — Workshop metadata (description uses Steam BBCode)
    README.md                  — End-user cheatsheet (Markdown)
```

---

## Build & deploy

```powershell
$env:ElinGamePath = [System.Environment]::GetEnvironmentVariable('ElinGamePath', 'User')
dotnet build WordFilterPlus/WordFilterPlus.csproj -c Release
# DLL is auto-copied to $ElinGamePath/Package/Mod_WordFilterPlus/ by the .csproj post-build step
```

---

## What's gitignored / should never be committed

- `obj/` — all build intermediates (cache files, nuget props, generated configs)
- `bin/` — build output DLLs and PDBs
- Game DLL references (copied from ElinGamePath at build time, never committed)

---

## Harmony patching

For general Harmony gotchas (nested classes, lambdas, call-site capture, overloads, etc.) see `Elin.Plugins/AGENTS.md`. This plugin is a concrete example of the `[ThreadStatic]` call-site capture pattern described there.
