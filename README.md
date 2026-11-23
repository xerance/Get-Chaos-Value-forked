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

Settings are under `Ignored Cell Settings`, with the 5x12 grid drawn at the bottom of the
plugin settings window:

- **Respect Ignored Inventory Cells** - items in an ignored cell are not added to the inventory value.
- **Hide Prices On Ignored Items** - also skip the price overlay on those items.
- **Sync Cells From HighlightedItems** - read the `IgnoredCells` grid out of the HighlightedItems
  settings file - normally `<HUD>/config/<profile>/HighlightedItems_settings.json`, auto-detected,
  re-read about once a second (int grids and boolean grids are both understood), or point at
  the file yourself. Cells coming from HighlightedItems are shown with a green box and cannot be
  unticked here; tick extra cells in the grid to ignore them in Ninja Price only.
- **Reload HighlightedItems Cells** / **Clear Own Ignored Cells** - force a re-read, or wipe the
  cells ticked in this plugin.

As in HighlightedItems, only the top left cell of a multi-cell item is checked. The stash value is
unaffected - this applies to the player inventory only.
