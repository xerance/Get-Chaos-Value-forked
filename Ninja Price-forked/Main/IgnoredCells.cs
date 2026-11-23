using ExileCore.PoEMemory.Elements.InventoryElements;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;

namespace Ninja_Price.Main;

/// <summary>
/// Support for ignoring player inventory cells, mirroring the "Ignored Inventory Slots" grid of
/// the HighlightedItems plugin (https://github.com/ravand1990/HighlightedItems). Items whose
/// top left cell is ignored are not counted towards the inventory value.
/// </summary>
public partial class Main
{
    public const int IgnoredCellRowCount = 5;
    public const int IgnoredCellColumnCount = 12;
    private const int IgnoredCellCount = IgnoredCellRowCount * IgnoredCellColumnCount;

    private readonly bool[,] _effectiveIgnoredCells = new bool[IgnoredCellRowCount, IgnoredCellColumnCount];
    private readonly bool[,] _highlightedItemsIgnoredCells = new bool[IgnoredCellRowCount, IgnoredCellColumnCount];
    private readonly Stopwatch _highlightedItemsReloadTimer = Stopwatch.StartNew();
    private string _highlightedItemsSettingsPath;
    private DateTime _highlightedItemsSettingsWriteTime;
    private string _highlightedItemsStatus = "not loaded yet";
    private bool _ignoredCellsDirty = true;
    private string _lastOwnIgnoredCellMask;

    #region mask helpers

    private static bool IsValidCell(int row, int column)
    {
        return row >= 0 && row < IgnoredCellRowCount && column >= 0 && column < IgnoredCellColumnCount;
    }

    private bool GetOwnIgnoredCell(int row, int column)
    {
        if (!IsValidCell(row, column))
            return false;

        var mask = Settings.IgnoredCellSettings.OwnIgnoredCellMask;
        var index = row * IgnoredCellColumnCount + column;
        return mask != null && index < mask.Length && mask[index] == '1';
    }

    private void SetOwnIgnoredCell(int row, int column, bool ignored)
    {
        if (!IsValidCell(row, column))
            return;

        var builder = new StringBuilder(Settings.IgnoredCellSettings.OwnIgnoredCellMask ?? "");
        while (builder.Length < IgnoredCellCount)
            builder.Append('0');
        builder.Length = IgnoredCellCount;
        builder[row * IgnoredCellColumnCount + column] = ignored ? '1' : '0';

        var mask = builder.ToString();
        Settings.IgnoredCellSettings.OwnIgnoredCellMask = mask.Contains('1') ? mask : "";
        _ignoredCellsDirty = true;
    }

    private void ClearOwnIgnoredCells()
    {
        Settings.IgnoredCellSettings.OwnIgnoredCellMask = "";
        _ignoredCellsDirty = true;
    }

    #endregion

    #region HighlightedItems settings file

    private IEnumerable<string> GetIgnoredCellSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { ConfigDirectory, DirectoryFullName })
        {
            var directory = start;
            for (var depth = 0; depth < 5 && !string.IsNullOrEmpty(directory); depth++)
            {
                if (seen.Add(directory))
                    yield return directory;

                directory = Path.GetDirectoryName(directory);
            }
        }
    }

    private string ResolveHighlightedItemsSettingsPath()
    {
        var configured = Settings.IgnoredCellSettings.HighlightedItemsSettingsPath?.Value;
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        var relativeCandidates = new[]
        {
            Path.Join("HighlightedItems", "settings.json"),
            Path.Join("config", "HighlightedItems", "settings.json"),
            Path.Join("Plugins", "Compiled", "HighlightedItems", "settings.json"),
            Path.Join("Plugins", "Source", "HighlightedItems", "settings.json"),
        };

        foreach (var root in GetIgnoredCellSearchRoots())
        {
            foreach (var relative in relativeCandidates)
            {
                var candidate = Path.Join(root, relative);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Tolerate renamed plugin folders, e.g. "HighlightedItems-master".
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root, "*ighlighted*"))
                {
                    var candidate = Path.Join(directory, "settings.json");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception)
            {
                // Unreadable directory, keep looking.
            }
        }

        return null;
    }

    /// <summary>Re-reads the HighlightedItems settings file if it changed (or if forced).</summary>
    private void ReloadHighlightedItemsIgnoredCells(bool force)
    {
        if (!Settings.IgnoredCellSettings.SyncFromHighlightedItems.Value)
        {
            if (force)
                _highlightedItemsStatus = "sync disabled";

            return;
        }

        if (force)
        {
            _highlightedItemsSettingsPath = null;
            _highlightedItemsSettingsWriteTime = default;
        }

        if (_highlightedItemsSettingsPath == null || !File.Exists(_highlightedItemsSettingsPath))
            _highlightedItemsSettingsPath = ResolveHighlightedItemsSettingsPath();

        if (_highlightedItemsSettingsPath == null)
        {
            if (ClearHighlightedItemsIgnoredCells())
                _ignoredCellsDirty = true;

            _highlightedItemsStatus = "settings file not found";
            return;
        }

        try
        {
            var writeTime = File.GetLastWriteTimeUtc(_highlightedItemsSettingsPath);
            if (!force && writeTime == _highlightedItemsSettingsWriteTime)
                return;

            _highlightedItemsSettingsWriteTime = writeTime;
            _ignoredCellsDirty = true;
            ClearHighlightedItemsIgnoredCells();

            if (JObject.Parse(File.ReadAllText(_highlightedItemsSettingsPath))["IgnoredCells"] is not JArray ignoredCells)
            {
                _highlightedItemsStatus = $"no IgnoredCells in {_highlightedItemsSettingsPath}";
                return;
            }

            var ignoredCount = 0;
            for (var row = 0; row < Math.Min(IgnoredCellRowCount, ignoredCells.Count); row++)
            {
                if (ignoredCells[row] is not JArray columns)
                    continue;

                for (var column = 0; column < Math.Min(IgnoredCellColumnCount, columns.Count); column++)
                {
                    if (columns[column].Type is not (JTokenType.Integer or JTokenType.Float or JTokenType.Boolean))
                        continue;

                    if (columns[column].Value<int>() <= 0)
                        continue;

                    _highlightedItemsIgnoredCells[row, column] = true;
                    ignoredCount++;
                }
            }

            _highlightedItemsStatus = $"{ignoredCount} cell(s) from {_highlightedItemsSettingsPath}";
        }
        catch (Exception ex)
        {
            ClearHighlightedItemsIgnoredCells();
            _ignoredCellsDirty = true;
            _highlightedItemsSettingsWriteTime = default;
            _highlightedItemsStatus = $"failed to read {_highlightedItemsSettingsPath}: {ex.Message}";
        }
    }

    private bool ClearHighlightedItemsIgnoredCells()
    {
        var hadAny = false;
        for (var row = 0; row < IgnoredCellRowCount; row++)
        for (var column = 0; column < IgnoredCellColumnCount; column++)
        {
            hadAny |= _highlightedItemsIgnoredCells[row, column];
            _highlightedItemsIgnoredCells[row, column] = false;
        }

        return hadAny;
    }

    #endregion

    /// <summary>
    /// Refreshes the cached grid. Cheap enough to call every frame: the settings file is only
    /// touched once per second, and only re-parsed when its timestamp changed.
    /// </summary>
    public void UpdateIgnoredCells()
    {
        if (Settings.IgnoredCellSettings.SyncFromHighlightedItems.Value && _highlightedItemsReloadTimer.ElapsedMilliseconds > 1000)
        {
            _highlightedItemsReloadTimer.Restart();
            ReloadHighlightedItemsIgnoredCells(false);
        }

        var ownMask = Settings.IgnoredCellSettings.OwnIgnoredCellMask ?? "";
        if (ownMask != _lastOwnIgnoredCellMask)
        {
            _lastOwnIgnoredCellMask = ownMask;
            _ignoredCellsDirty = true;
        }

        if (!_ignoredCellsDirty)
            return;

        _ignoredCellsDirty = false;
        var syncEnabled = Settings.IgnoredCellSettings.SyncFromHighlightedItems.Value;
        for (var row = 0; row < IgnoredCellRowCount; row++)
        for (var column = 0; column < IgnoredCellColumnCount; column++)
        {
            _effectiveIgnoredCells[row, column] = GetOwnIgnoredCell(row, column) ||
                                                  (syncEnabled && _highlightedItemsIgnoredCells[row, column]);
        }
    }

    public bool IsIgnoredCell(int row, int column)
    {
        return IsValidCell(row, column) && _effectiveIgnoredCells[row, column];
    }

    /// <summary>
    /// True when the item sits in an ignored player inventory cell. Like HighlightedItems, only the
    /// top left cell of the item is looked at.
    /// </summary>
    public bool IsInIgnoredInventoryCell(CustomItem item)
    {
        if (!Settings.IgnoredCellSettings.Enable.Value)
            return false;

        try
        {
            var inventoryItem = item?.Element?.AsObject<NormalInventoryItem>();
            if (inventoryItem == null)
                return false;

            return IsIgnoredCell(inventoryItem.InventPosY, inventoryItem.InventPosX);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        DrawIgnoredCellsSettings();
    }

    private void DrawIgnoredCellsSettings()
    {
        ImGui.BeginChild("##NinjaPriceIgnoredCellsMain", new Vector2(ImGui.GetContentRegionAvail().X, 230f), ImGuiChildFlags.Border,
            ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.Text("Ignored Inventory Cells (checked = ignored, not counted in the inventory value)");
        ImGui.Text(Settings.IgnoredCellSettings.SyncFromHighlightedItems.Value
            ? $"HighlightedItems: {_highlightedItemsStatus}"
            : "HighlightedItems sync is off, only the cells ticked here are ignored");
        ImGui.Text("Cells with a green box come from HighlightedItems and cannot be unticked here.");

        var contentRegionAvail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("##NinjaPriceIgnoredCellsGrid", new Vector2(contentRegionAvail.X, contentRegionAvail.Y), ImGuiChildFlags.Border,
            ImGuiWindowFlags.NoScrollWithMouse);

        for (var row = 0; row < IgnoredCellRowCount; row++)
        {
            for (var column = 0; column < IgnoredCellColumnCount; column++)
            {
                var fromHighlightedItems = Settings.IgnoredCellSettings.SyncFromHighlightedItems.Value &&
                                           _highlightedItemsIgnoredCells[row, column];
                if (fromHighlightedItems)
                    ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.5f, 0.15f, 1f));

                var ignored = GetOwnIgnoredCell(row, column) || fromHighlightedItems;
                if (ImGui.Checkbox($"##NinjaPriceIgnoredCell{row}_{column}", ref ignored) && !fromHighlightedItems)
                    SetOwnIgnoredCell(row, column, ignored);

                if (fromHighlightedItems)
                    ImGui.PopStyleColor();

                if (column < IgnoredCellColumnCount - 1)
                    ImGui.SameLine();
            }
        }

        ImGui.EndChild();
        ImGui.EndChild();
    }
}
