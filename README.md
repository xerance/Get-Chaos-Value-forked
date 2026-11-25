# Get-Chaos-Value-forked

Fork of [instantsc/Get-Chaos-Value](https://github.com/instantsc/Get-Chaos-Value), the live upstream.
The plugin loads as **Ninja Price-forked** so it can sit side by side with the original in the same
HUD: the plugin name, assembly, namespace and plugin-bridge keys are all suffixed, so it gets its
own settings file (`config/<profile>/Ninja_Price_Forked.Main_settings.json`) and its own
`NinjaData` cache instead of fighting the original over them.
ExileAPI Plugin for instant price data comparison.

https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/635847-poehud-plugin-ninja-pricer.html

What does it do?
This plugin download poe.ninja API as a local structured data file onto your PC.
The plugin can then use this data to compare with the data from ExileAPI and your client to give the user instant price check on item such as:
- Currency
- Divination Card
- Essence
- Fragment
- Incubator
- Oil
- Prophecy
- Resonator
- Fossil
- Scarab
- Vial
- Delirium Orb
- Map
- Unique

The plugin can also show the overall worth of a stash tab or inventory.

Item that aren't available on poe.ninja show 0c as price.
Item that have multiple variant (ex: Vessel of Vinktar) show a range between the lowest and highest cost. The plugin can't differentiate between them.

## Fork change: ignored inventory cells

This fork leaves items that sit in **ignored inventory cells** out of the inventory value
total, using the same cell grid as the
[HighlightedItems](https://github.com/ravand1990/HighlightedItems) plugin.

Settings are under `Ignored Cell Settings`, with a read-only copy of the 5x12 grid drawn at the
bottom of the plugin settings window:

- **Respect HighlightedItems' Ignored Cells** - items in an ignored cell are not added to the
  inventory value.
- **Hide Prices On Ignored Items** - also skip the price overlay on those items.
- **HighlightedItems settings file** - normally auto-detected at
  `<HUD>/config/<profile>/HighlightedItems_settings.json` (newest profile wins; both the int grid
  of older HighlightedItems builds and the boolean grid of current ones are understood), or point
  at the file yourself.
- **Reload Ignored Cells Now** - force a re-read; otherwise the file is checked about once a second
  and re-parsed whenever its timestamp changes.

The grid has a single owner: HighlightedItems. There is deliberately no second grid to keep in
sync, and the checkboxes shown here only mirror the file - edit the cells in HighlightedItems and
this plugin follows. Two-way editing is not offered because the HUD writes each plugin's whole
settings file from memory when it saves, so anything written into HighlightedItems' file from the
outside would be silently overwritten.

The cell of an item is worked out from the inventory geometry (grid origin plus the item's own cell
size), falling back to the deprecated `InventPosX/Y` properties. As in HighlightedItems, only the
top left cell of a multi-cell item is checked. The stash value is unaffected - this applies to the
player inventory only.

The settings window also shows what the last inventory pass saw ("12 item(s), 3 in ignored cells"),
which is the quickest way to tell whether the grid is being applied.

## Fork change: tier-aware map pricing

poe.ninja lists generic maps with the tier in the name (`Map (Tier 16)`,
`Al-Hezmin Map (Tier 16)`), while named atlas maps keep their own name
(`Vaal Temple Map`). The in-game base name never carries a tier, so upstream's
exact name match found nothing for generic maps and they were priced at 0 (and
therefore not drawn at all). The lookup now tries `<name> (Tier N)` first and
falls back to the plain name.

## Fork change: manual price overrides

poe.ninja's aggregation sometimes produces a nonsense chaos value for a heavily listed
line - the plain `Map (Tier 16)` entry has read 2c, 20.2c and 23.2c within a single day
while every other map tier sat at 1-3c - and there is no second source to cross-check it
against. Rather than guess, the fork lets you pin a value yourself.

Edit `price_overrides.txt` in the plugin's config folder
(`<HUD>/config/Ninja_Price_Forked.Main/`), one override per line:

```
# key = chaos value
map-tier-16-t0-gen-24 = 2
Map (Tier 16) = 2
```

The key is matched case-insensitively against the item's poe.ninja `DetailsId` first,
then its unique name, then its base name; hover an item with debug logging on to see all
three. Blank lines and `#` comments are ignored, and a value is read with either `.` or
`,` as the decimal separator. Stackable items are valued per item and multiplied by the
stack size.

The file is created on first launch, but you do not have to touch it: the settings panel
has a row editor. **Add Row** appends a blank pair, **Add Last Hovered Item** fills one in
from the item you hovered most recently (its DetailsId and its price for a single item, so a
hovered stack of 20 Gemcutter's Prisms fills in the price of one), **Remove** drops
a row, **Save** writes the file and re-applies it, and **Discard Changes** re-reads from
disk. Comments above the first entry are preserved when saving; comments below it are not.

**Reload Overrides Now** re-reads the file if you edited it by hand, and unticking **Use
Manual Price Overrides** falls back to poe.ninja everywhere without deleting anything. An
overridden item shows `Price: manual override` in the hovered-item debug block.
