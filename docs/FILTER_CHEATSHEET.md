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

| Syntax | Matches |
|--------|---------|
| `@fire` | Item has any element whose internal name or display name contains `fire` |
| `@identified` | Item is identified (`thing.IsIdentified == true`) |
| `-@fire` | Block items that have `fire` elements |

Requires the item to be identified (same rule as the Ctrl+F enc-search below).
`@identified` never requires identification to evaluate — it is the identification check itself.

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

### Examples

```
@fire                     show items with fire element
@identified               show only identified items
rarity:>=good             show Superior and above
-rarity:=normal           hide Normal rarity
sword @fire rarity:>normal  show identified swords with fire element, above Normal
-@identified              hide unidentified items from distribution
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

Calls `Thing.MatchEncSearch(s)` which:
- **Identified items**: returns `true` if any element on the item has an internal name or display name containing `s` with a non-zero value.
- **Gene items** (`TraitGene`): scans DNA element names regardless of identification; skips Brain and Inferior DNA types.
- **Unidentified items**: always returns `false`.

| Syntax | Behaviour |
|--------|-----------|
| `fire` | Normal text search — find items/NPCs with "fire" in name |
| `@fire` | Enc-search — find identified items with a "fire" element |
| `@speed` | Find items enchanted with speed-related elements |

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
