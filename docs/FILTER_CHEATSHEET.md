# Elin Filter Cheatsheet

Two separate filter systems exist in the game. Neither is aware of the other.

---

## Distribution / Stock Filter (storage chest Ctrl+F)

This is what **WordFilterPlus** extends. The filter box is the search bar on a
storage container's distribution window.

### Vanilla operators (always available)

| Syntax | Behaviour |
|--------|-----------|
| `sword` | **Include** — show item only if its name contains `sword` |
| `-sword` | **Block** — hide item if its name contains `sword` |
| `+sword` | **Pass** — show item unconditionally if its name contains `sword`, skip all other tokens |
| `sword axe` | Multiple tokens, space-separated; all Include tokens must match |

The `-` / `+` stripping happens in `BuildFilter()` before any token reaches our code.
Plain-text tokens are a simple case-insensitive `Contains()` on the display name.

### WordFilterPlus extended operators

#### Element / enchantment search — `@query`

Matches items where any element's **internal alias** or **display name** contains the query (case-insensitive substring). Requires the item to be identified, except `@identified` which is the identification check itself.

**Elemental attack/resistance** — both the attack and resistance share the same word, so one query catches both:

| Query | Catches |
|-------|---------|
| `@fire` | Fire Attack (`eleFire`) + Fire Resistance (`resFire`) |
| `@cold` | Cold Attack + Cold Resistance |
| `@lightning` | Lightning Attack + Lightning Resistance |
| `@nether` | Nether Attack + Nether Resistance |
| `@acid` | Acid Attack + Acid Resistance |
| `@darkness` | Darkness Attack + Darkness Resistance |
| `@chaos` | Chaos Attack + Chaos Resistance |
| `@poison` | Poison Attack + Poison Resistance |
| `@mind` | Mind Attack + Mind Resistance |
| `@nerve` | Nerve Attack + Nerve Resistance |
| `@ether` | Ether Attack + Ether Resistance |
| `@sound` | Sound Attack + Sound Resistance |
| `@cut` | Cut Attack + Cut Resistance |
| `@holy` | Holy Attack + Holy Veil ability |
| `@magic` | Magic Attack + Magic Resistance + Magic (MAG) attribute |
| `@ele` | **Any** elemental attack enchantment |
| `@res` | **Any** resistance enchantment |
| `-@fire` | Block items that have any fire element |

**Attributes** — use the 2–3 letter stat code or the full word:

| Query | Attribute |
|-------|-----------|
| `@str` or `@strength` | Strength |
| `@dex` or `@dexterity` | Dexterity |
| `@spd` or `@speed` | Speed |
| `@per` or `@perception` | Perception |
| `@wil` or `@will` | Will |
| `@mag` | Magic / Mana (also matches magic enchantments) |
| `@cha` or `@charisma` | Charisma |
| `@end` or `@endurance` | Endurance |
| `@luc` or `@luck` | Luck |
| `@ler` or `@learning` | Learning |

**Skills:**

| Query | Skill |
|-------|-------|
| `@fishing` | Fishing |
| `@mining` | Mining |
| `@taming` | Taming |
| `@swimming` | Swimming |
| `@lumberjack` | Lumberjacking |
| `@acrobat` | Acrobat |
| `@lockpicking` | Lockpicking |
| `@spot` | Spot Hidden |
| `@digging` | Digging |
| `@gathering` | Gathering |
| `@tilling` | Tilling |
| `@weightlifting` | Weightlifting |
| `@travel` | Travel |

**Abilities and stats:**

| Query | What it finds |
|-------|--------------|
| `@teleport` | Teleport ability |
| `@heal` | Heal ability + Healing |
| `@detox` | Detox ability |
| `@bind` | Bind ability |
| `@return` | Return ability |
| `@sense` | Sense Treasure ability |
| `@hit` or `@tohit` | ToHit bonus |
| `@life` | Life bonus |
| `@mana` | Mana bonus |

**Identification:**

| Query | What it finds |
|-------|--------------|
| `@identified` | Only identified items (evaluated without requiring identification) |
| `-@identified` | Block identified items (i.e. show only unidentified) |

#### Rarity filter — `rarity:expression`

| Syntax | Matches |
|--------|---------|
| `rarity:good` | Rarity equals Superior |
| `rarity:=good` | Same (explicit equals) |
| `rarity:>good` | Rarity strictly above Superior |
| `rarity:>=good` | Rarity Superior or better |
| `rarity:=>good` | Same as `>=` (forgiving alias) |
| `rarity:<legendary` | Rarity strictly below Legendary |
| `rarity:<=legendary` | Rarity Legendary or worse |
| `rarity:=<legendary` | Same as `<=` (forgiving alias) |
| `-rarity:=normal` | Block Normal-rarity items |

##### Accepted rarity names

| Internal name | Display name alias | Enum value |
|---------------|--------------------|-----------|
| `crude` | *(none)* | −1 |
| `normal` | *(none)* | 0 |
| `superior` | `good` | 1 |
| `legendary` | `miracle` | 2 |
| `mythical` | `godly` | 3 |
| `artifact` | `artefact`, `precious` | 4 |

Both internal names and display aliases are accepted, case-insensitive.

### Combined examples

```
@fire                           show items with any fire element
-@identified                    show only unidentified items
rarity:>=good                   show Superior and above
-rarity:=normal                 hide Normal rarity items
sword @fire                     swords with fire attack or resistance
sword @fire rarity:>normal      fire swords above Normal rarity
@nether @str                    items with nether element AND Strength bonus
@res -@fire                     any resistance EXCEPT fire
rarity:artifact                 artifacts only
-rarity:<=normal                hide Crude and Normal (same as rarity:>normal)
```

---

## Map / World Ctrl+F Search (WidgetSearch)

This is the **search widget** that highlights items and NPCs on the map.
It is entirely separate — WordFilterPlus does **not** affect it.

### Plain search (no prefix)

Matches if `item.Name` (display name, lowercased) **or** `item.source.GetSearchName()` (internal search name) contains your query.

Also matches NPCs by name in town zones.

Only works in home zones (PC faction / tent / town).

### Enc-search — `@query` prefix

Type `@` before your query to switch to element/enchantment mode.

Calls `Thing.MatchEncSearch(s)` which checks two fields per element:
- `element.source.name` — the internal alias (e.g. `eleFire`, `resCold`, `casting`, `speed`)
- `element.source.GetName()` — the localized display name (e.g. "Fire Attack", "Cold Resistance")

If either field contains the query string (case-insensitive substring), the item matches.

Rules:
- **Identified items**: matches if any element with a non-zero value matches. `@fire` hits both fire *attack* enchantments and fire *resistance* because both contain "fire".
- **Gene items** (`TraitGene`): scans DNA element names regardless of identification; skips Brain and Inferior DNA types.
- **Unidentified items**: always returns `false`.

| Syntax | What it finds |
|--------|--------------|
| `fire` | Items/NPCs with "fire" in display name |
| `@fire` | Fire Attack + Fire Resistance (alias `eleFire`/`resFire` both contain "fire") |
| `@cold` | Cold Attack + Cold Resistance |
| `@nether` | Nether Attack + Nether Resistance |
| `@lightning` | Lightning Attack + Lightning Resistance |
| `@acid` | Acid Attack + Acid Resistance |
| `@darkness` | Darkness Attack + Darkness Resistance |
| `@chaos` | Chaos Attack + Chaos Resistance |
| `@poison` | Poison Attack + Poison Resistance |
| `@magic` | Magic Attack + Magic Resistance + Magic attribute |
| `@mind` | Mind Attack + Mind Resistance |
| `@ether` | Ether Attack + Ether Resistance |
| `@holy` | Holy Attack + Holy Veil ability |
| `@ele` | Any elemental attack enchantment |
| `@res` | Any resistance enchantment |
| `@str` | Strength attribute (alias `STR`) |
| `@spd` or `@speed` | Speed attribute (alias `SPD`) |
| `@per` | Perception (alias `PER`) |
| `@wil` | Will (alias `WIL`) |
| `@mag` | Magic/Mana (alias `MAG`) |
| `@cha` | Charisma (alias `CHA`) |
| `@end` | Endurance (alias `END`) |
| `@luc` | Luck (alias `LUC`) |
| `@fishing` | Fishing skill |
| `@mining` | Mining skill |
| `@taming` | Taming skill |
| `@lumberjack` | Lumberjacking skill |
| `@acrobat` | Acrobat skill |
| `@spot` | Spot Hidden skill |
| `@teleport` | Teleport ability |
| `@heal` | Heal/Healing |
| `@detox` | Detox ability |
| `@bind` | Bind ability |

The `@` prefix is consumed before the query is passed down; the query itself is lowercased.
Full-width `＠` is also accepted as an alias.

### Key differences from the distribution filter

| | Distribution filter | Map Ctrl+F |
|--|---------------------|-----------|
| Plugin-extended? | **Yes** (WordFilterPlus) | No |
| Scope | Items in storage chest | All items + NPCs on map |
| `@` meaning | WordFilterPlus element query | Vanilla enc-search |
| Rarity filter | `rarity:` syntax | Not available |
| Identified-only check | `@identified` token | Implicit in enc-search |
| Negation | `-token` prefix | Not available |
