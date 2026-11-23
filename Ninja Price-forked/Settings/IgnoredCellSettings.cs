using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace Ninja_Price_Forked.Settings;

[Submenu(CollapsedByDefault = true)]
public class IgnoredCellSettings
{
    [Menu("Respect HighlightedItems' Ignored Cells",
        "Items that sit in a cell ignored by HighlightedItems are left out of the inventory value total")]
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    [Menu("Hide Prices On Ignored Items",
        "Also skip drawing the price overlay on items in ignored cells")]
    public ToggleNode HideIgnoredItemPrices { get; set; } = new ToggleNode(true);

    [Menu("HighlightedItems settings file",
        "Leave empty to auto-detect, normally <HUD>/config/<profile>/HighlightedItems_settings.json")]
    public TextNode HighlightedItemsSettingsPath { get; set; } = new TextNode("");

    [Menu("Reload Ignored Cells Now")]
    public ButtonNode ReloadHighlightedItemsCells { get; set; } = new ButtonNode();
}
