using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Ninja_Price_Forked.Main;

// Fork addition: let the user pin a chaos value for lines whose poe.ninja price is wrong.
// poe.ninja's aggregation occasionally produces a nonsense number for a heavily listed line
// (the plain "Map (Tier 16)" entry has read anywhere from 2c to 23c in a single day while every
// other tier sat at 1-3c), and there is no second source to cross-check it against.
//
// The file stays the source of truth so it survives a settings wipe, but it can be edited
// entirely from the settings panel.
public partial class Main
{
    public const string PriceOverrideFileName = "price_overrides.txt";

    private const string PriceOverrideHeader = """
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
        # This file can be edited from the plugin's settings panel. Comments above the first
        # entry are kept when saving from there; comments below it are not.
        """;

    private sealed class PriceOverrideRow
    {
        public string Key = "";
        public string Value = "";
    }

    private readonly Dictionary<string, double> _priceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PriceOverrideRow> _priceOverrideRows = [];
    private string _priceOverrideFileHeader = PriceOverrideHeader;
    private string _priceOverrideStatus = "not loaded";
    private bool _priceOverridesDirty;

    private string PriceOverrideFilePath => Path.Combine(ConfigDirectory, PriceOverrideFileName);

    public void ReloadPriceOverrides(bool createIfMissing)
    {
        _priceOverrides.Clear();
        _priceOverrideRows.Clear();
        _priceOverridesDirty = false;

        try
        {
            var path = PriceOverrideFilePath;

            if (!File.Exists(path))
            {
                if (!createIfMissing)
                {
                    _priceOverrideStatus = "no file yet - add a row and save";
                    return;
                }

                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(path, PriceOverrideHeader + Environment.NewLine);
                _priceOverrideFileHeader = PriceOverrideHeader;
                _priceOverrideStatus = "created an empty override file";
                return;
            }

            var headerLines = new List<string>();
            var seenEntry = false;
            var badLines = 0;

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    if (!seenEntry) headerLines.Add(rawLine.TrimEnd());
                    continue;
                }

                seenEntry = true;

                if (!TryParseOverrideLine(line, out var key, out var value))
                {
                    badLines++;
                    continue;
                }

                _priceOverrides[key] = value;
                _priceOverrideRows.Add(new PriceOverrideRow
                {
                    Key = key,
                    Value = value.ToString(CultureInfo.InvariantCulture),
                });
            }

            if (headerLines.Count > 0)
                _priceOverrideFileHeader = string.Join(Environment.NewLine, headerLines).TrimEnd();

            _priceOverrideStatus = badLines == 0
                ? $"{_priceOverrides.Count} override(s) loaded"
                : $"{_priceOverrides.Count} override(s) loaded, {badLines} unreadable line(s) skipped";
        }
        catch (Exception exception)
        {
            _priceOverrides.Clear();
            _priceOverrideRows.Clear();
            _priceOverrideStatus = $"failed to read overrides: {exception.Message}";
        }
    }

    private static bool TryParseOverrideLine(string line, out string key, out double value)
    {
        key = null;
        value = 0;

        var separator = line.LastIndexOf('=');
        if (separator <= 0) return false;

        key = line[..separator].Trim();
        if (key.Length == 0) return false;

        // Accept both 2.5 and 2,5 - the HUD runs under whatever locale the user has.
        var rawValue = line[(separator + 1)..].Trim().Replace(',', '.');
        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void SavePriceOverrides()
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.IsNullOrWhiteSpace(_priceOverrideFileHeader)
                ? PriceOverrideHeader
                : _priceOverrideFileHeader);
            builder.AppendLine();

            var written = 0;
            var skipped = 0;

            foreach (var row in _priceOverrideRows)
            {
                var key = row.Key?.Trim();
                var rawValue = row.Value?.Trim().Replace(',', '.');

                if (string.IsNullOrEmpty(key) ||
                    !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    skipped++;
                    continue;
                }

                builder.AppendLine($"{key} = {value.ToString(CultureInfo.InvariantCulture)}");
                written++;
            }

            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(PriceOverrideFilePath, builder.ToString());

            // Re-read so what is applied is exactly what landed on disk.
            ReloadPriceOverrides(false);

            if (skipped > 0)
                _priceOverrideStatus = $"saved {written} override(s), dropped {skipped} incomplete row(s)";
        }
        catch (Exception exception)
        {
            _priceOverrideStatus = $"failed to save overrides: {exception.Message}";
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

    private void DrawPriceOverrideSettings()
    {
        // Note: the nuint buffer sizes below are the Hexa.NET.ImGui signature, which is what
        // the HUD ships. ImGui.NET takes a uint there instead.
        ImGui.Separator();
        ImGui.BeginChild("##NinjaPricePriceOverrides", new Vector2(ImGui.GetContentRegionAvail().X, 230f));

        ImGui.Text("Manual Price Overrides");
        ImGui.Text($"Status: {_priceOverrideStatus}{(_priceOverridesDirty ? "  (unsaved changes)" : "")}");
        ImGui.Text("Key = poe.ninja DetailsId, unique name or base name. Value is in chaos.");

        if (ImGui.Button("Add Row"))
        {
            _priceOverrideRows.Add(new PriceOverrideRow());
            _priceOverridesDirty = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Save"))
        {
            SavePriceOverrides();
        }

        ImGui.SameLine();
        if (ImGui.Button("Discard Changes"))
        {
            ReloadPriceOverrides(false);
        }

        // HoveredItem is null by the time the mouse reaches this panel, so this uses the last
        // item that was hovered in game.
        var lastHovered = LastHoveredItem;
        var lastHoveredKey = lastHovered == null
            ? null
            : !string.IsNullOrWhiteSpace(lastHovered.PriceData.DetailsId)
                ? lastHovered.PriceData.DetailsId
                : lastHovered.BaseName;

        ImGui.SameLine();
        if (ImGui.Button("Add Last Hovered Item") && !string.IsNullOrWhiteSpace(lastHoveredKey))
        {
            _priceOverrideRows.Add(new PriceOverrideRow
            {
                Key = lastHoveredKey,
                Value = lastHovered.PriceData.MinChaosValue.ToString("0.##", CultureInfo.InvariantCulture),
            });
            _priceOverridesDirty = true;
        }

        ImGui.Text($"Last hovered: {(string.IsNullOrWhiteSpace(lastHoveredKey) ? "nothing yet" : lastHoveredKey)}");

        ImGui.Separator();
        ImGui.BeginChild("##NinjaPricePriceOverrideRows", ImGui.GetContentRegionAvail());

        var keyWidth = Math.Max(160f, ImGui.GetContentRegionAvail().X - 190f);
        PriceOverrideRow rowToRemove = null;

        for (var i = 0; i < _priceOverrideRows.Count; i++)
        {
            var row = _priceOverrideRows[i];

            ImGui.SetNextItemWidth(keyWidth);
            var key = row.Key ?? "";
            if (ImGui.InputText($"##NinjaPriceOverrideKey{i}", ref key, (nuint)128))
            {
                row.Key = key;
                _priceOverridesDirty = true;
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f);
            var value = row.Value ?? "";
            if (ImGui.InputText($"##NinjaPriceOverrideValue{i}", ref value, (nuint)32))
            {
                row.Value = value;
                _priceOverridesDirty = true;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Remove##NinjaPriceOverrideRemove{i}"))
            {
                rowToRemove = row;
            }
        }

        if (rowToRemove != null)
        {
            _priceOverrideRows.Remove(rowToRemove);
            _priceOverridesDirty = true;
        }

        ImGui.EndChild();
        ImGui.EndChild();
    }
}
