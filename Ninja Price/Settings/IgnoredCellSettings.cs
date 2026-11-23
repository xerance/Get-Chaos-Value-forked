using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;

namespace Ninja_Price.Settings;

[Submenu(CollapsedByDefault = true)]
public class IgnoredCellSettings
{
    [Menu("Respect Ignored Inventory Cells",
        "Items that sit in an ignored inventory cell are left out of the inventory value total")]
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    [Menu("Hide Prices On Ignored Items",
        "Also skip drawing the price overlay on items in ignored cells")]
    public ToggleNode HideIgnoredItemPrices { get; set; } = new ToggleNode(true);

    [Menu("Sync Cells From HighlightedItems",
        "Read the IgnoredCells grid out of the HighlightedItems plugin settings file so both plugins ignore the same cells")]
    public ToggleNode SyncFromHighlightedItems { get; set; } = new ToggleNode(true);

    [Menu("HighlightedItems settings.json Path",
        "Leave empty to auto-detect inside the HUD folder (config/HighlightedItems/settings.json and friends)")]
    public TextNode HighlightedItemsSettingsPath { get; set; } = new TextNode("");

    [Menu("Reload HighlightedItems Cells")]
    public ButtonNode ReloadHighlightedItemsCells { get; set; } = new ButtonNode();

    [Menu("Clear Own Ignored Cells")]
    public ButtonNode ClearOwnIgnoredCells { get; set; } = new ButtonNode();

    // 60 characters of '0'/'1', row-major (5 rows x 12 columns). Stored as a string because
    // Newtonsoft cannot round-trip a multidimensional array.
    [JsonProperty]
    public string OwnIgnoredCellMask { get; set; } = "";
}
