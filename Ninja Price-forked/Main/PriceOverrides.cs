using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Ninja_Price_Forked.Main;

// Fork addition: let the user pin a chaos value for lines whose poe.ninja price is wrong.
// poe.ninja's aggregation occasionally produces a nonsense number for a heavily listed line
// (the plain "Map (Tier 16)" entry has read anywhere from 2c to 23c in a single day while every
// other tier sat at 1-3c), and there is no second source to cross-check it against.
public partial class Main
{
    public const string PriceOverrideFileName = "price_overrides.txt";

    private const string PriceOverrideTemplate = """
        # Ninja Price-forked manual price overrides.
        #
        # One override per line, as:  key = chaos value
        # The key is matched, case-insensitively, against the item's poe.ninja DetailsId
        # first, then its unique name, then its base name. Hover an item with debug logging
        # on to see all three.
        #
        # Blank lines and lines starting with # are ignored. Remove an entry to go back to
        # poe.ninja's price. Stackable items are valued per item and multiplied by the stack.
        #
        # Examples:
        # map-tier-16-t0-gen-24 = 2
        # Map (Tier 16) = 2

        """;

    private readonly Dictionary<string, double> _priceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private string _priceOverrideStatus = "not loaded";

    private string PriceOverrideFilePath => Path.Combine(ConfigDirectory, PriceOverrideFileName);

    public void ReloadPriceOverrides(bool createIfMissing)
    {
        _priceOverrides.Clear();

        try
        {
            var path = PriceOverrideFilePath;

            if (!File.Exists(path))
            {
                if (!createIfMissing)
                {
                    _priceOverrideStatus = $"no file at {path}";
                    return;
                }

                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(path, PriceOverrideTemplate);
                _priceOverrideStatus = $"created {path}";
                return;
            }

            var badLines = 0;

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;

                var separator = line.LastIndexOf('=');
                if (separator <= 0)
                {
                    badLines++;
                    continue;
                }

                var key = line[..separator].Trim();
                var rawValue = line[(separator + 1)..].Trim();

                // Accept both 2.5 and 2,5 - the HUD runs under whatever locale the user has.
                if (key.Length == 0 ||
                    !double.TryParse(rawValue.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    badLines++;
                    continue;
                }

                _priceOverrides[key] = value;
            }

            _priceOverrideStatus = badLines == 0
                ? $"{_priceOverrides.Count} override(s) from {path}"
                : $"{_priceOverrides.Count} override(s) from {path}, {badLines} line(s) skipped";
        }
        catch (Exception exception)
        {
            _priceOverrides.Clear();
            _priceOverrideStatus = $"failed to read overrides: {exception.Message}";
        }
    }

    // Runs after the poe.ninja lookup, so an override always wins.
    private void ApplyPriceOverride(CustomItem item)
    {
        if (item == null || _priceOverrides.Count == 0) return;
        if (!Settings.PriceOverrideSettings.Enable.Value) return;

        var keys = new[] { item.PriceData.DetailsId, item.UniqueName, item.BaseName };

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!_priceOverrides.TryGetValue(key.Trim(), out var value)) continue;

            var stackSize = Math.Max(1, item.CurrencyInfo.StackSize);
            item.PriceData.MinChaosValue = value * stackSize;
            item.PriceData.MaxChaosValue = value * stackSize;
            item.PriceData.IsOverridden = true;
            return;
        }
    }

    private IEnumerable<string> PriceOverrideLinesForDisplay() =>
        _priceOverrides.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key} = {x.Value.ToString(CultureInfo.InvariantCulture)}c");
}
