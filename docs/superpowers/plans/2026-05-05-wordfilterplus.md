# WordFilterPlus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend Elin's distribution word filter (`Window.SaveData.IsFilterPass`) to support `@operator` element matching, `rarity:` comparisons, and `identified:` predicates across all four call sites — covering manual auto-dump, delegated farming AI, container haul routing, and on-walk auto-pick.

**Architecture:** A Harmony prefix on `Window.SaveData.IsFilterPass` takes full ownership when the filter string contains extended tokens, using a `[ThreadStatic]` slot to receive the `Thing` injected by IL Transpilers on each of the 4 call-site methods. Plain-token-only filters get zero overhead (early exit). No save-data format changes.

**Tech Stack:** C# `netstandard2.0`, BepInEx 5, Harmony 2, Elin game assemblies via `ElinGamePath` env var.

---

## Context you need before starting

### Key game files (read-only reference, do not modify)

- `Elin-Decompiled/Elin/Plugins.UI/Window.cs` — `Window.SaveData.IsFilterPass`, `BuildFilter`, `FilterResult` enum
- `Elin-Decompiled/Elin/TaskDump.cs` — `ListThingsToPut` (contains 3 `IsFilterPass` call sites in lambdas)
- `Elin-Decompiled/Elin/Zone.cs` ~2160 — `TryAddThingInSharedContainer` → local function `SearchDest`
- `Elin-Decompiled/Elin/ThingContainer.cs` ~475 — local function `TrySearchContainer`
- `Elin-Decompiled/Elin/Chara.cs` ~3121 — direct call in method body
- `Elin-Decompiled/Elin/Thing.cs` ~2154 — `Thing.MatchEncSearch(string s)` — borrow this logic
- `Elin-Decompiled/Elin/Rarity.cs` — `enum Rarity { Crude=-1, Normal=0, Superior=1, Legendary=2, Mythical=3, Artifact=4 }`

### How `BuildFilter` works (important — do NOT patch it)

`BuildFilter()` splits `filter` on `,` and assigns each token an `option`:
- `0` = include (no prefix)
- `1` = block (`-` prefix, stripped from token)
- `2` = pass-unconditionally (`+` prefix, stripped)

Extended tokens (`@operator`, `rarity:`, `identified:`) pass through `BuildFilter` unchanged because `@`, `r`, and `i` are not recognised prefix chars. `-@operator` gets option=1 with the text `@operator` — the evaluator detects the leading `@` to identify it as an extended token with block polarity.

### `IsFilterPass` current logic (replicate for plain tokens)

```csharp
// Simplified: returns Pass/Block/PassWithoutFurtherTest
for (int i = 0; i < filterStrs.Length; i++) {
    switch (filterOptions[i]) {
        case 0: if (text.Contains(filterStrs[i])) flag = true; flag2 = true; break;
        case 1: if (text.Contains(filterStrs[i])) return Block;           break;
        case 2: if (text.Contains(filterStrs[i])) return PassWithoutFurtherTest; break;
    }
}
if (!flag && flag2) return Block;
return Pass;
```

### How `MatchEncSearch` works (replicate, not call — avoid reflection cost)

```csharp
// From Thing.cs — unidentified items return false
if (!IsIdentified) return false;
foreach (var el in elements.dict.Values) {
    if (el.Value != 0 &&
       (el.source.name.ToLower().Contains(s) || el.source.GetName().ToLower().Contains(s)))
        return true;
}
return false;
```

### The transpiler pattern you'll use in Tasks 5 & 6

Each of the 4 game methods contains an instruction sequence like:
```
ldloc <thing>          // Thing t pushed
... (NameStyle args)
callvirt Thing.GetName  // → string
callvirt Window.SaveData.IsFilterPass  // call to patch
```

The transpiler inserts a `dup` + `stsfld FilterContext.Current` immediately after the `ldloc <thing>` instruction and before `GetName`. After the `IsFilterPass` call it inserts `ldnull` + `stsfld FilterContext.Current` to clear.

Use `HarmonyLib` `CodeInstruction` and `System.Reflection.Emit.OpCodes`.

---

## File map

| File | Action | Responsibility |
|---|---|---|
| `WordFilterPlus.sln` | Create | Solution file |
| `WordFilterPlus/WordFilterPlus.csproj` | Create | Project definition, `netstandard2.0` |
| `WordFilterPlus/Directory.Build.props` | Create | BepInEx/Harmony/game assembly refs via `ElinGamePath` |
| `WordFilterPlus/AsmInfo.cs` | Create | `AssemblyVersion` from `ModInfo.Version` |
| `WordFilterPlus/Plugin.cs` | Create | BepInEx entry, `Harmony.CreateAndPatchAll` |
| `WordFilterPlus/Filter/FilterContext.cs` | Create | `[ThreadStatic] Thing? Current` + `Set`/`Clear` helpers |
| `WordFilterPlus/Filter/ExtendedFilter.cs` | Create | `HasExtendedTokens`, `EvaluateFilter` — all token logic |
| `WordFilterPlus/Patches/IsFilterPassPatch.cs` | Create | Harmony prefix on `Window.SaveData.IsFilterPass` |
| `WordFilterPlus/Patches/CallSitePatches.cs` | Create | Transpilers on 4 game methods to inject `FilterContext.Current` |
| `.tools/invoke_serena_project_index.ps1` | Create | Copy verbatim from `Elin.Plugins/.tools/` |
| `.tools/reindex_serena.ps1` | Create | Copy verbatim from `Elin.Plugins/.tools/` |
| `.tools/serena_output_filter.ps1` | Create | Copy verbatim from `Elin.Plugins/.tools/` |
| `.vscode/tasks.json` | Create | Serena reindex task |
| `reindex_serena.bat` | Create | Copy verbatim from `Elin.Plugins/reindex_serena.bat` |

---

## Task 1: Project scaffold

**Files:**
- Create: `WordFilterPlus.sln`
- Create: `WordFilterPlus/WordFilterPlus.csproj`
- Create: `WordFilterPlus/Directory.Build.props`

- [ ] **Step 1: Create the solution file**

Run from `c:\Git\Elin.SearchPlugin`:
```powershell
dotnet new sln -n WordFilterPlus
```
Expected output: `The template "Solution File" was created successfully.`

- [ ] **Step 2: Create the project**

```powershell
New-Item -ItemType Directory -Path WordFilterPlus
dotnet new classlib -n WordFilterPlus -o WordFilterPlus --framework netstandard2.0
Remove-Item WordFilterPlus\Class1.cs
dotnet sln add WordFilterPlus\WordFilterPlus.csproj
```

- [ ] **Step 3: Replace the generated .csproj**

Replace the contents of `WordFilterPlus/WordFilterPlus.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Platforms>AnyCPU</Platforms>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>WordFilterPlus</RootNamespace>
    <AssemblyName>WordFilterPlus</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateDependencyFile>false</GenerateDependencyFile>
    <OutputPath>$(ElinGamePath)\BepInEx\plugins\WordFilterPlus\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
    <Deterministic>false</Deterministic>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="package\**" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create `Directory.Build.props`**

Create `WordFilterPlus/Directory.Build.props`:

```xml
<Project>
    <PropertyGroup Condition="'$(ElinGamePath.Trim())' == '' AND '$(OS)' == 'Windows_NT'">
        <ElinGamePath>C:\Program Files (x86)\Steam\steamapps\common\Elin</ElinGamePath>
    </PropertyGroup>
    <PropertyGroup>
        <NoWarn>1701;1702;8620;8604</NoWarn>
    </PropertyGroup>
    <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
    </PropertyGroup>
    <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
        <DebugType>portable</DebugType>
        <Optimize>true</Optimize>
        <DefineConstants>TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
    <ItemGroup>
        <Reference Include="$(ElinGamePath)\BepInEx\core\0Harmony.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\BepInEx\core\BepInEx.Core.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\BepInEx\core\BepInEx.Unity.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\BepInEx\core\Mono*.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\ClassLibrary2.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\Elin.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\UnityEngine*.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\mscorlib.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\netstandard.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\System.dll">
            <Private>False</Private>
        </Reference>
        <Reference Include="$(ElinGamePath)\Elin_Data\Managed\System.Core.dll">
            <Private>False</Private>
        </Reference>
    </ItemGroup>
</Project>
```

- [ ] **Step 5: Verify restore succeeds**

```powershell
$env:ElinGamePath = [System.Environment]::GetEnvironmentVariable('ElinGamePath','User')
dotnet restore WordFilterPlus\WordFilterPlus.csproj
```

Expected: `Restore succeeded.` If it fails with missing game DLLs, verify `ElinGamePath` points to your Elin installation.

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "chore: project scaffold"
```

---

## Task 2: Plugin entry and assembly info

**Files:**
- Create: `WordFilterPlus/Plugin.cs`
- Create: `WordFilterPlus/AsmInfo.cs`

- [ ] **Step 1: Create `Plugin.cs`**

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;

namespace WordFilterPlus;

public static class ModInfo
{
    public const string Guid    = "dk.elinplugins.wordfilterplus";
    public const string Name    = "WordFilterPlus";
    public const string Version = "1.0.0";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance;

    private void Awake()
    {
        Instance = this;
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModInfo.Guid);
        LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded.");
    }

    internal static void LogDebug(object msg, [CallerMemberName] string caller = "") =>
        Instance?.Logger.LogDebug($"[{caller}] {msg}");

    internal static void LogInfo(object msg) =>
        Instance?.Logger.LogInfo(msg);

    internal static void LogError(object msg) =>
        Instance?.Logger.LogError(msg);
}
```

- [ ] **Step 2: Create `AsmInfo.cs`**

```csharp
using System.Reflection;
using WordFilterPlus;

[assembly: AssemblyVersion($"{ModInfo.Version}.*")]
[assembly: AssemblyFileVersion(ModInfo.Version)]
[assembly: AssemblyTitle(ModInfo.Name)]
```

- [ ] **Step 3: Build to verify it compiles**

```powershell
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Debug --no-restore
```

Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add WordFilterPlus\Plugin.cs WordFilterPlus\AsmInfo.cs
git commit -m "chore: plugin entry and assembly info"
```

---

## Task 3: FilterContext

**Files:**
- Create: `WordFilterPlus/Filter/FilterContext.cs`

- [ ] **Step 1: Create `FilterContext.cs`**

```csharp
namespace WordFilterPlus.Filter;

/// <summary>
/// Thread-local slot that holds the Thing currently being evaluated by IsFilterPass.
/// Set by transpiler-injected code at each call site; cleared immediately after the call.
/// </summary>
internal static class FilterContext
{
    [System.ThreadStatic]
    internal static Thing? Current;
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Debug --no-restore
```

Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 3: Commit**

```powershell
git add WordFilterPlus\Filter\FilterContext.cs
git commit -m "feat: add FilterContext thread-local slot"
```

---

## Task 4: ExtendedFilter — token detection and evaluation

**Files:**
- Create: `WordFilterPlus/Filter/ExtendedFilter.cs`

This is the core logic file. It owns:
- Detecting whether a filter string has any extended tokens
- Evaluating the full filter (plain + extended) against a `Thing`
- Rarity comparison, `identified:`, `@operator` matching

- [ ] **Step 1: Create `ExtendedFilter.cs`**

```csharp
using System;
using System.Collections.Generic;
using Window = global::Window;

namespace WordFilterPlus.Filter;

internal static class ExtendedFilter
{
    // Tokens parsed by BuildFilter; option 0=include, 1=block, 2=pass-unconditional
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
            if (s.Length == 0) continue;
            if (s[0] == '@') return true;
            if (s.StartsWith("rarity:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("identified:", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Full filter evaluation replacing IsFilterPass when extended tokens are present.
    /// Replicates the plain-token logic of the original and adds extended token evaluation.
    /// </summary>
    internal static Window.SaveData.FilterResult EvaluateFilter(
        string[] filterStrs, int[] filterOptions, string nameText, Thing? thing)
    {
        bool anyInclude      = false;   // at least one option-0 token matched
        bool hasIncludeToken = false;   // at least one option-0 token exists

        for (int i = 0; i < filterStrs.Length; i++)
        {
            var token  = filterStrs[i];
            var option = filterOptions[i];
            if (token.Length == 0) continue;

            bool matched = EvaluateToken(token, nameText, thing);

            switch (option)
            {
                case OptionBlock:
                    if (matched) return Window.SaveData.FilterResult.Block;
                    break;
                case OptionPass:
                    if (matched) return Window.SaveData.FilterResult.PassWithoutFurtherTest;
                    break;
                case OptionInclude:
                    hasIncludeToken = true;
                    if (matched) anyInclude = true;
                    break;
            }
        }

        if (!anyInclude && hasIncludeToken) return Window.SaveData.FilterResult.Block;
        return Window.SaveData.FilterResult.Pass;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool EvaluateToken(string token, string nameText, Thing? thing)
    {
        // @operator — element/enchantment search
        if (token.Length > 1 && token[0] == '@')
        {
            var query = token.Substring(1).ToLower();
            return thing != null && MatchElements(thing, query);
        }

        // rarity: comparisons
        if (token.StartsWith("rarity:", StringComparison.OrdinalIgnoreCase))
        {
            return thing != null && MatchRarity(token.Substring(7), thing.rarity);
        }

        // identified:
        if (token.StartsWith("identified:", StringComparison.OrdinalIgnoreCase))
        {
            var val = token.Substring(11).Trim();
            if (thing == null) return false;
            bool wantIdentified = val.Equals("true", StringComparison.OrdinalIgnoreCase);
            return thing.IsIdentified == wantIdentified;
        }

        // Plain name token — standard Contains check
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
    /// </summary>
    private static bool MatchRarity(string expr, Rarity itemRarity)
    {
        string op;
        string name;

        if (expr.StartsWith(">="))      { op = ">="; name = expr.Substring(2); }
        else if (expr.StartsWith("<=")) { op = "<="; name = expr.Substring(2); }
        else if (expr.StartsWith("!=")) { op = "!="; name = expr.Substring(2); }
        else if (expr.StartsWith(">"))  { op = ">";  name = expr.Substring(1); }
        else if (expr.StartsWith("<"))  { op = "<";  name = expr.Substring(1); }
        else if (expr.StartsWith("="))  { op = "=";  name = expr.Substring(1); }
        else                            { op = "=";  name = expr; }

        if (!TryParseRarity(name.Trim(), out var target)) return false;

        return op switch
        {
            "="  => itemRarity == target,
            "!=" => itemRarity != target,
            ">"  => itemRarity >  target,
            ">=" => itemRarity >= target,
            "<"  => itemRarity <  target,
            "<=" => itemRarity <= target,
            _    => false,
        };
    }

    private static bool TryParseRarity(string name, out Rarity result)
    {
        result = Rarity.Normal;
        return Enum.TryParse(name, ignoreCase: true, out result);
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Debug --no-restore
```

Expected: `Build succeeded.  0 Error(s)`

If you see "Rarity does not exist in namespace" — it is a top-level game type in `Elin.dll`, no namespace qualifier needed. Verify `Directory.Build.props` references `Elin_Data\Managed\Elin.dll`.

- [ ] **Step 3: Commit**

```powershell
git add WordFilterPlus\Filter\ExtendedFilter.cs
git commit -m "feat: extended filter token detection and evaluation"
```

---

## Task 5: IsFilterPassPatch

**Files:**
- Create: `WordFilterPlus/Patches/IsFilterPassPatch.cs`

This prefix intercepts `Window.SaveData.IsFilterPass`. If the filter has no extended tokens it returns `true` (run original). Otherwise it evaluates using `ExtendedFilter` and skips the original.

- [ ] **Step 1: Create `IsFilterPassPatch.cs`**

```csharp
using HarmonyLib;
using WordFilterPlus.Filter;
using static Window;

namespace WordFilterPlus.Patches;

[HarmonyPatch(typeof(SaveData), nameof(SaveData.IsFilterPass))]
internal static class IsFilterPassPatch
{
    // Prefix: returns false (skip original) when extended tokens are present.
    // __instance = the Window.SaveData being evaluated
    // text       = item display name (already passed to original)
    // __result   = output: set by us when we take ownership
    [HarmonyPrefix]
    private static bool Prefix(SaveData __instance, string text, ref SaveData.FilterResult __result)
    {
        // filterStrs is a cached property backed by BuildFilter(); safe to access.
        if (!ExtendedFilter.HasExtendedTokens(__instance.filterStrs))
            return true; // no extended tokens — let original run, zero overhead

        __result = ExtendedFilter.EvaluateFilter(
            __instance.filterStrs,
            __instance.filterOptions,
            text,
            FilterContext.Current);

        return false; // skip original
    }
}
```

> **Note:** `Window.SaveData` is a nested class. Harmony targets nested classes via the outer type: `typeof(Window.SaveData)` — the nested class is accessible as `Window.SaveData` in C#. The `using static Window` brings `SaveData` into scope for brevity. If the compiler complains about the alias, use `typeof(Window.SaveData)` directly in the attribute.

- [ ] **Step 2: Build**

```powershell
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Debug --no-restore
```

Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 3: Commit**

```powershell
git add WordFilterPlus\Patches\IsFilterPassPatch.cs
git commit -m "feat: IsFilterPass prefix patch"
```

---

## Task 6: CallSitePatches — inject FilterContext.Current via transpilers

**Files:**
- Create: `WordFilterPlus/Patches/CallSitePatches.cs`

Each of the 4 game methods has one or more `IsFilterPass(t.GetName(...))` calls. A transpiler on each method inserts `FilterContext.Current = t` immediately before `GetName` is called (so `t` is still on the stack), and clears it immediately after `IsFilterPass` returns.

The IL sequence to find:
```
ldloc <thing>     ← Thing t is loaded here
ldc.i4.s 2        ← NameStyle.Full = 2
ldc.i4.1          ← num = 1
callvirt Thing::GetName(NameStyle, int32)
callvirt Window/SaveData::IsFilterPass(string)
```

The transpiler inserts after the `ldloc <thing>` instruction:
```
dup
stsfld FilterContext.Current
```

And after `callvirt IsFilterPass`:
```
ldnull
stsfld FilterContext.Current   (as Thing — null assignment clears the slot)
```

- [ ] **Step 1: Create `CallSitePatches.cs`**

```csharp
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using WordFilterPlus.Filter;

namespace WordFilterPlus.Patches;

/// <summary>
/// Transpilers on the 4 game methods that call Window.SaveData.IsFilterPass.
/// Each transpiler injects FilterContext.Current = t before the call and clears it after.
/// This gives IsFilterPassPatch access to the Thing being evaluated without changing signatures.
/// </summary>
internal static class CallSitePatches
{
    private static readonly MethodInfo _isFilterPass =
        AccessTools.Method(typeof(Window.SaveData), nameof(Window.SaveData.IsFilterPass));

    private static readonly FieldInfo _currentField =
        AccessTools.Field(typeof(FilterContext), nameof(FilterContext.Current));

    // -------------------------------------------------------------------------
    // Patch registrations
    // -------------------------------------------------------------------------

    [HarmonyPatch(typeof(TaskDump), nameof(TaskDump.ListThingsToPut))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileListThingsToPut(
        IEnumerable<CodeInstruction> instructions) =>
        InjectFilterContext(instructions, nameof(TaskDump.ListThingsToPut));

    [HarmonyPatch(typeof(Zone), nameof(Zone.TryAddThingInSharedContainer))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileTryAddThingInSharedContainer(
        IEnumerable<CodeInstruction> instructions) =>
        InjectFilterContext(instructions, nameof(Zone.TryAddThingInSharedContainer));

    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.GetDest))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileThingContainerGetDest(
        IEnumerable<CodeInstruction> instructions) =>
        InjectFilterContext(instructions, "ThingContainer.GetDest");

    // Chara auto-pick: the IsFilterPass call is inside _Move (line ~3121),
    // inside the `if (IsPC)` block that triggers on each step.
    [HarmonyPatch(typeof(Chara), nameof(Chara._Move))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileCharaMove(
        IEnumerable<CodeInstruction> instructions) =>
        InjectFilterContext(instructions, "Chara._Move");

    // -------------------------------------------------------------------------
    // Shared transpiler logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans for every `callvirt IsFilterPass` and:
    ///   1. Finds the preceding `ldloc` (the Thing) that ends up as the receiver of GetName.
    ///   2. Inserts `dup; stsfld FilterContext.Current` after that ldloc.
    ///   3. Inserts `ldnull; stsfld FilterContext.Current` after the IsFilterPass call.
    /// </summary>
    private static IEnumerable<CodeInstruction> InjectFilterContext(
        IEnumerable<CodeInstruction> instructions, string methodLabel)
    {
        var codes = new List<CodeInstruction>(instructions);
        int injected = 0;

        for (int i = 0; i < codes.Count; i++)
        {
            // Find callvirt IsFilterPass
            if (!IsIsFilterPassCall(codes[i])) continue;

            // Walk backwards to find the ldloc/ldarg that loaded the Thing.
            // The Thing is the first arg to GetName; GetName is called just before IsFilterPass.
            // Pattern: ldloc.X, [ldc.i4 NameStyle.Full], [ldc.i4.1], callvirt GetName, callvirt IsFilterPass
            // Find the GetName call (should be i-1 ignoring any ldnull / nop junk)
            int getNameIdx = FindPriorCall(codes, i, "GetName");
            if (getNameIdx < 0)
            {
                Plugin.LogError($"[CallSitePatches:{methodLabel}] Could not find GetName before IsFilterPass at index {i}");
                continue;
            }

            // The Thing ldloc is before the NameStyle and num arguments.
            // GetName(NameStyle, int) has 2 args + implicit this → walk back 3 from GetName call.
            int thingLoadIdx = FindThingLoad(codes, getNameIdx);
            if (thingLoadIdx < 0)
            {
                Plugin.LogError($"[CallSitePatches:{methodLabel}] Could not find Thing ldloc before GetName at index {getNameIdx}");
                continue;
            }

            // Insert AFTER the ldloc (index thingLoadIdx+1): dup; stsfld Current
            codes.Insert(thingLoadIdx + 1, new CodeInstruction(OpCodes.Stsfld, _currentField));
            codes.Insert(thingLoadIdx + 1, new CodeInstruction(OpCodes.Dup));

            // Adjust i for the 2 inserted instructions
            i += 2;

            // Insert AFTER the (now-shifted) IsFilterPass call: ldnull; stsfld Current
            int afterCall = i + 1;
            codes.Insert(afterCall, new CodeInstruction(OpCodes.Stsfld, _currentField));
            codes.Insert(afterCall, new CodeInstruction(OpCodes.Ldnull));

            i += 2; // skip past the clear instructions
            injected++;
        }

        if (injected == 0)
            Plugin.LogError($"[CallSitePatches:{methodLabel}] No IsFilterPass call sites found — transpiler had no effect. Check game version.");
        else
            Plugin.LogDebug($"[CallSitePatches:{methodLabel}] Injected FilterContext around {injected} IsFilterPass call(s).");

        return codes;
    }

    private static bool IsIsFilterPassCall(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) &&
        ci.operand is MethodInfo m && m == _isFilterPass;

    private static int FindPriorCall(List<CodeInstruction> codes, int beforeIdx, string methodName)
    {
        for (int j = beforeIdx - 1; j >= 0; j--)
        {
            var ci = codes[j];
            if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) &&
                ci.operand is MethodInfo m && m.Name == methodName)
                return j;
        }
        return -1;
    }

    /// <summary>
    /// Walks backwards from getNameIdx to find the ldloc/ldarg that loaded the Thing instance.
    /// GetName(NameStyle, int) has 2 explicit args; the implicit `this` (the Thing) is arg 0.
    /// We expect: ldloc.X (Thing), ldc.i4 (NameStyle), ldc.i4 (num), callvirt GetName
    /// So the Thing load is 3 positions before the GetName call.
    /// </summary>
    private static int FindThingLoad(List<CodeInstruction> codes, int getNameIdx)
    {
        // Count back past the 2 explicit args
        int skipped = 0;
        for (int j = getNameIdx - 1; j >= 0; j--)
        {
            var op = codes[j].opcode;
            // skip nop / label-only instructions
            if (op == OpCodes.Nop) continue;
            skipped++;
            if (skipped == 2)
            {
                // The next backwards step should be the Thing load
                for (int k = j - 1; k >= 0; k--)
                {
                    var op2 = codes[k].opcode;
                    if (op2 == OpCodes.Nop) continue;
                    if (IsLoadInstruction(op2)) return k;
                    break;
                }
                break;
            }
        }
        return -1;
    }

    private static bool IsLoadInstruction(OpCode op) =>
        op == OpCodes.Ldloc   || op == OpCodes.Ldloc_S ||
        op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
        op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 ||
        op == OpCodes.Ldarg   || op == OpCodes.Ldarg_S  ||
        op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1  ||
        op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3;
}
```

> **Important — before building:** Open `Elin-Decompiled/Elin/Chara.cs` and go to line ~3095. Find the enclosing method name (the one containing `dataPick.IsFilterPass`). Replace `"Tick"` in the `Chara` transpiler patch with that method name. The method is likely something like `TryAutoPickNearby`, `OnStep`, or a similar per-step AI method.
>
> Also verify the `ThingContainer` method name: the `TrySearchContainer` local function is inside an outer method. Open `Elin-Decompiled/Elin/ThingContainer.cs` ~470 and find the outer method declaration. Replace `"GetDest"` with the correct name if different.

- [ ] **Step 2: Build**

```powershell
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Debug --no-restore
```

Expected: `Build succeeded.  0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add WordFilterPlus\Patches\CallSitePatches.cs
git commit -m "feat: call site transpilers for FilterContext injection"
```

---

## Task 7: Serena tooling and VS Code task

**Files:**
- Create: `.tools/invoke_serena_project_index.ps1`
- Create: `.tools/reindex_serena.ps1`
- Create: `.tools/serena_output_filter.ps1`
- Create: `.vscode/tasks.json`
- Create: `reindex_serena.bat`

- [ ] **Step 1: Copy .tools scripts from Elin.Plugins**

```powershell
New-Item -ItemType Directory -Path .tools
Copy-Item "C:\Git\Elin.Plugins\.tools\invoke_serena_project_index.ps1" .tools\
Copy-Item "C:\Git\Elin.Plugins\.tools\reindex_serena.ps1"              .tools\
Copy-Item "C:\Git\Elin.Plugins\.tools\serena_output_filter.ps1"        .tools\
```

- [ ] **Step 2: Create `reindex_serena.bat`**

```batch
@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0.tools\reindex_serena.ps1" %*
exit /b %errorlevel%
```

- [ ] **Step 3: Create `.vscode/tasks.json`**

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "Serena: Reindex WordFilterPlus",
            "type": "shell",
            "command": "powershell",
            "args": [
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                "${workspaceFolder}\\.tools\\reindex_serena.ps1"
            ],
            "options": {
                "cwd": "${workspaceFolder}"
            },
            "group": "build"
        }
    ]
}
```

- [ ] **Step 4: Commit**

```powershell
git add .tools .vscode reindex_serena.bat
git commit -m "chore: Serena tooling and VS Code task"
```

---

## Task 8: In-game verification

No automated unit tests are feasible for game-coupled code. Verify manually in-game.

- [ ] **Step 1: Build in Release and verify output file exists**

```powershell
$env:ElinGamePath = [System.Environment]::GetEnvironmentVariable('ElinGamePath','User')
dotnet build WordFilterPlus\WordFilterPlus.csproj -c Release --no-restore
Test-Path "$env:ElinGamePath\BepInEx\plugins\WordFilterPlus\WordFilterPlus.dll"
```

Expected: `True`

- [ ] **Step 2: Verify plugin loads**

Launch Elin. Check `Elin\BepInEx\LogOutput.log` for:
```
[Info   : WordFilterPlus] WordFilterPlus 1.0.0 loaded.
```
If not present, check for errors in the same log.

- [ ] **Step 3: Verify transpiler injection succeeded**

In the same log, check for lines like:
```
[Debug  : WordFilterPlus] [TranspileListThingsToPut] Injected FilterContext around N IsFilterPass call(s).
```
All 4 transpilers should log successful injection (N > 0). An `[Error]` line here means the method name was wrong — fix `CallSitePatches.cs` and rebuild.

- [ ] **Step 4: Test @operator filter**

1. Open a container chest, set `AutodumpFlag` to `distribution`.
2. Set its word filter to `@swimming`.
3. Acquire an item with a swimming-related enchantment and an item without.
4. Run auto-dump (press the dump hotkey or wait for delegated worker).
5. Verify: only the item with the swimming enchantment is deposited.

- [ ] **Step 5: Test rarity filter**

1. Set a chest's filter to `rarity:>=legendary`.
2. Have items of various rarities in inventory.
3. Run auto-dump.
4. Verify: only Legendary, Mythical, and Artifact items are deposited.

- [ ] **Step 6: Test identified filter**

1. Set a chest's filter to `identified:false`.
2. Have both identified and unidentified items in inventory.
3. Run auto-dump.
4. Verify: only unidentified items are deposited.

- [ ] **Step 7: Test mixed filter**

1. Set a chest's filter to `sword,@fire,-shield`.
2. Have: an iron sword with fire enchantment, an iron sword without, a fire shield.
3. Run auto-dump.
4. Verify: fire-enchanted sword is deposited; plain sword follows plain name logic; fire shield is excluded.

- [ ] **Step 8: Test plain filter is unaffected**

1. Set a chest's filter to `sword` (plain, no extended tokens).
2. Confirm auto-dump still works exactly as before — the DEBUG log should NOT show filter evaluation (early-exit path taken).

- [ ] **Step 9: Test delegated farming path**

1. Assign a follower with haul/farm duty.
2. Set a chest with `@operator` or `rarity:` filter.
3. Let the follower deposit items.
4. Verify the filter is respected (correct items land in the chest).

- [ ] **Step 10: Final commit**

```powershell
git add .
git commit -m "feat: WordFilterPlus 1.0.0 — extended distribution word filter"
```
