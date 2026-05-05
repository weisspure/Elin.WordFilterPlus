# WordFilterPlus

Extended filter support for Elin's item distribution system. Adds **rarity and identification status filtering** to the vanilla filter system. All vanilla operators (`-` block, `+` force-pass, `"` quotes) now work with extended filters too.

## New Extended Filters

### Rarity Matching

Match items by rarity level with comparison operators. You can use either **internal names** (superior, legendary) or **display names** (good, miracle):

- `rarity:normal` — Match Normal rarity (internal name)
- `rarity:good` — Match Superior rarity (display name, same as `rarity:superior`)
- `rarity:=miracle` — Match Legendary rarity (display name)
- `rarity:>good` — Match rarer than Superior (Legendary/miracle, Mythical/godly, Artifact/artefact)
- `rarity:<miracle` — Match less rare than Legendary
- `rarity:>=good` — Match Superior or rarer
- `rarity:<=miracle` — Match Legendary or less rare

**Available rarity levels:**
- `crude` (no display name equivalent)
- `normal` → displays as "normal"
- `superior` or `good` → displays as "good"
- `legendary` or `miracle` → displays as "miracle"
- `mythical` or `godly` → displays as "godly"
- `artifact`, `artefact`, or `precious` → displays as "artefact"

### Identification Status

Match identified or unidentified items:

- `@identified` — Match only identified items
- `-@identified` — Block identified items (keep unidentified)

## Vanilla Filters

These already exist in Elin's vanilla filter system:

- `text` — Match items with text in the name (e.g., `sword`, `iron`)
- `-text` — Block items with text in the name (vanilla blocking)
- `+text` — Force include items with text (vanilla force-pass)

Note: Vanilla does **not** support `@enchantment` or other property filters in the distribution system.

## Blocking & Including
**All Elin operators work with extended filters:**

- `-` prefix — **Block** items matching the filter (exclude them)
- `+` prefix — **Force-pass** items matching the filter (include immediately)
- `"` or `'` — Strip quotes (vanilla text handling)

These work with all extended tokens (rarity, @identified, @enchantment):

- `-rarity:normal` — Block all Normal rarity items (keep Superior+) **[extended]**
- `+rarity:>=good` — Force include Superior or better items **[extended]**
- `-@identified` — Block all identified items (keep unidentified) **[extended]**
- `-@fire` — Block all fire-enchanted items **[extended]**
- `-sword` — Block items with "sword" in name **[vanilla text]**

Without prefix, filters **include** items matching the criteria.

## Examples

**Extended: Include identified Superior+ items, exclude fire enchantments:**
```
@identified,rarity:>=superior
-@fire
```

**Mixed: Unidentified items named "sword":**
```
-@identified,sword
```

**Extended: Block common items, force include Superior+:**
```
-rarity:normal,+rarity:>=good
```

**Vanilla: Block items with "sword" in name:**
```
-sword
```

## Combining Filters

Separate multiple filters with **commas** (`,`) or Japanese commas (`、`):

```
@identified,rarity:>=good,-@fire
```

Or one per line in the filter UI (each line is a separate filter field).

The game's filter logic applies them as follows:
- All **include** filters must match (or if none exist, all non-blocked items pass)
- Any **block** (`-`) filter triggers immediate exclusion
- Any **pass** (`+`) filter triggers immediate inclusion

**How operators work with extended filters:**
WordFilterPlus properly integrates with Elin's native filter operators. The game preprocesses `-` and `+` prefixes before our code sees them, converting them into internal options. Our plugin respects these options for all tokens (vanilla and extended), giving you consistent behavior everywhere.

This means:
- `rarity:>=good`, `-rarity:normal`, and `+rarity:artifact` all use the same rarity comparison logic
- `-@fire` and `-sword` both use the same blocking mechanism
- Operators work predictably across all filter types

Standard Elin behavior — WordFilterPlus just extends the token types and respects the operator system.