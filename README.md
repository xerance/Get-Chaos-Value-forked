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
