using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace Ninja_Price_Forked.Settings;

[Submenu(CollapsedByDefault = true)]
public class PriceOverrideSettings
{
    [Menu("Use Manual Price Overrides",
        "Replace poe.ninja's chaos value with your own for the lines you list in price_overrides.txt")]
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    [Menu("Reload Overrides Now",
        "Re-read price_overrides.txt from the plugin's config folder")]
    public ButtonNode ReloadOverrides { get; set; } = new ButtonNode();
}
