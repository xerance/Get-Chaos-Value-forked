using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Enums;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Color = SharpDX.Color;

namespace Ninja_Price_Forked.Main;

/// <summary>
/// Leaves items that sit in an ignored player inventory cell out of the inventory value.
/// The cell grid is owned by the HighlightedItems plugin
/// (https://github.com/ravand1990/HighlightedItems) and read out of its settings file, so there is
/// a single place to edit it and the two plugins can never disagree.
/// </summary>
public partial class Main
{
    public const int IgnoredCellRowCount = 5;
    public const int IgnoredCellColumnCount = 12;

    private readonly bool[,] _ignoredCells = new bool[IgnoredCellRowCount, IgnoredCellColumnCount];
    private readonly Stopwatch _highlightedItemsReloadTimer = Stopwatch.StartNew();
    private string _highlightedItemsSettingsPath;
    private DateTime _highlightedItemsSettingsWriteTime;
    private string _highlightedItemsStatus = "not loaded yet";
    private string _lastInventoryScanStatus = "inventory not open yet";
    private int _lastScanItemCount;
    private int _lastScanIgnoredCount;

    private static bool IsValidCell(int row, int column)
    {
        return row >= 0 && row < IgnoredCellRowCount && column >= 0 && column < IgnoredCellColumnCount;
    }

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

    private static IEnumerable<string> EnumerateSelfAndChildren(string directory)
    {
        yield return directory;
        foreach (var child in EnumerateDirectoriesSafe(directory, "*"))
            yield return child;
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, pattern);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private string ResolveHighlightedItemsSettingsPath()
    {
        var configured = Settings.IgnoredCellSettings.HighlightedItemsSettingsPath?.Value;
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        // The HUD keeps plugin settings in <HUD>/config/<profile>/<PluginId>_settings.json, so the
        // file normally lives next to ours, e.g. config/global/HighlightedItems_settings.json. Older
        // layouts kept a settings.json inside the plugin's own folder, hence the fallbacks below.
        var relativeCandidates = new[]
        {
            Path.Join("HighlightedItems", "settings.json"),
            Path.Join("config", "HighlightedItems", "settings.json"),
            Path.Join("Plugins", "Compiled", "HighlightedItems", "settings.json"),
            Path.Join("Plugins", "Source", "HighlightedItems", "settings.json"),
        };

        foreach (var root in GetIgnoredCellSearchRoots())
        {
            // Newest match wins: with several profiles, the active one is the one being written to.
            var newestMatch = (Path: (string)null, WriteTime: DateTime.MinValue);
            foreach (var directory in EnumerateSelfAndChildren(root))
            {
                foreach (var file in EnumerateFilesSafe(directory, "*ighlighted*tems*_settings.json"))
                {
                    var writeTime = File.GetLastWriteTimeUtc(file);
                    if (writeTime > newestMatch.WriteTime)
                        newestMatch = (file, writeTime);
                }
            }

            if (newestMatch.Path != null)
                return newestMatch.Path;

            foreach (var relative in relativeCandidates)
            {
                var candidate = Path.Join(root, relative);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Tolerate renamed plugin folders, e.g. "HighlightedItems-master".
            foreach (var directory in EnumerateDirectoriesSafe(root, "*ighlighted*"))
            {
                var candidate = Path.Join(directory, "settings.json");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    /// <summary>Re-reads the HighlightedItems settings file if it changed (or if forced).</summary>
    private void ReloadHighlightedItemsIgnoredCells(bool force)
    {
        if (force)
        {
            _highlightedItemsSettingsPath = null;
            _highlightedItemsSettingsWriteTime = default;
        }

        if (_highlightedItemsSettingsPath == null || !File.Exists(_highlightedItemsSettingsPath))
            _highlightedItemsSettingsPath = ResolveHighlightedItemsSettingsPath();

        if (_highlightedItemsSettingsPath == null)
        {
            ClearIgnoredCells();
            _highlightedItemsStatus = "settings file not found";
            return;
        }

        try
        {
            var writeTime = File.GetLastWriteTimeUtc(_highlightedItemsSettingsPath);
            if (!force && writeTime == _highlightedItemsSettingsWriteTime)
                return;

            _highlightedItemsSettingsWriteTime = writeTime;
            ClearIgnoredCells();

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
                    // Older HighlightedItems builds store an int grid, current ones store booleans.
                    var token = columns[column];
                    var ignored = token.Type switch
                    {
                        JTokenType.Boolean => token.Value<bool>(),
                        JTokenType.Integer or JTokenType.Float => token.Value<double>() > 0,
                        _ => false,
                    };

                    if (!ignored)
                        continue;

                    _ignoredCells[row, column] = true;
                    ignoredCount++;
                }
            }

            _highlightedItemsStatus = $"{ignoredCount} cell(s) from {_highlightedItemsSettingsPath}";
        }
        catch (Exception ex)
        {
            ClearIgnoredCells();
            _highlightedItemsSettingsWriteTime = default;
            _highlightedItemsStatus = $"failed to read {_highlightedItemsSettingsPath}: {ex.Message}";
        }
    }

    private void ClearIgnoredCells()
    {
        for (var row = 0; row < IgnoredCellRowCount; row++)
        for (var column = 0; column < IgnoredCellColumnCount; column++)
            _ignoredCells[row, column] = false;
    }

    #endregion

    /// <summary>
    /// Cheap enough to call every frame: the settings file is only touched once per second, and
    /// only re-parsed when its timestamp changed.
    /// </summary>
    public void UpdateIgnoredCells()
    {
        if (_highlightedItemsReloadTimer.ElapsedMilliseconds > 1000)
        {
            _highlightedItemsReloadTimer.Restart();
            ReloadHighlightedItemsIgnoredCells(false);
        }

        _lastScanItemCount = 0;
        _lastScanIgnoredCount = 0;
    }

    public bool IsIgnoredCell(int row, int column)
    {
        return IsValidCell(row, column) && _ignoredCells[row, column];
    }

    /// <summary>
    /// Works out which inventory cell an item's top left corner sits in. Geometry is preferred over
    /// NormalInventoryItem.InventPosX/Y (both deprecated in ExileCore): the grid element gives the
    /// origin, and the cell size comes from the item itself (its rect divided by its cell size), so
    /// any padding on the grid element cannot throw the result off.
    /// </summary>
    private bool TryGetInventoryCell(CustomItem item, out int row, out int column)
    {
        row = -1;
        column = -1;

        var itemElement = item?.Element;
        if (itemElement == null)
            return false;

        NormalInventoryItem inventoryItem = null;
        try
        {
            inventoryItem = itemElement.AsObject<NormalInventoryItem>();
        }
        catch (Exception)
        {
            // Not an inventory item element, geometry below may still work.
        }

        var gridElement = GameController.Game.IngameState.IngameUi.InventoryPanel?[InventoryIndex.PlayerInventory];
        var gridRect = gridElement?.GetClientRectCache ?? default;
        var itemRect = itemElement.GetClientRectCache;

        if (gridRect.Width > 0 && gridRect.Height > 0 && itemRect.Width > 0 && itemRect.Height > 0)
        {
            var cellWidth = gridRect.Width / IgnoredCellColumnCount;
            var cellHeight = gridRect.Height / IgnoredCellRowCount;

            // A 1x1 item is exactly one cell, a 2x3 item exactly six, so the item's own rect is the
            // most reliable ruler available.
            if (inventoryItem is { ItemWidth: > 0, ItemHeight: > 0 })
            {
                cellWidth = itemRect.Width / inventoryItem.ItemWidth;
                cellHeight = itemRect.Height / inventoryItem.ItemHeight;
            }

            if (cellWidth > 0 && cellHeight > 0)
            {
                column = (int)MathF.Round((itemRect.X - gridRect.X) / cellWidth);
                row = (int)MathF.Round((itemRect.Y - gridRect.Y) / cellHeight);

                if (IsValidCell(row, column))
                    return true;
            }
        }

        // Fall back to the deprecated properties if the geometry gave us nothing usable.
        try
        {
            if (inventoryItem == null)
                return false;

#pragma warning disable CS0612 // deprecated, but it is the only other source of the position
            row = inventoryItem.InventPosY;
            column = inventoryItem.InventPosX;
#pragma warning restore CS0612
            return IsValidCell(row, column);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// True when the item sits in an ignored player inventory cell. Like HighlightedItems, only the
    /// top left cell of a multi-cell item is looked at.
    /// </summary>
    public bool IsInIgnoredInventoryCell(CustomItem item)
    {
        if (!Settings.IgnoredCellSettings.Enable.Value)
            return false;

        _lastScanItemCount++;

        if (!TryGetInventoryCell(item, out var row, out var column))
        {
            _lastInventoryScanStatus = $"{_lastScanItemCount} item(s), {_lastScanIgnoredCount} ignored (last item: cell unknown)";
            return false;
        }

        var ignored = IsIgnoredCell(row, column);
        if (ignored)
            _lastScanIgnoredCount++;

        if (Settings.DebugSettings.EnableDebugLogging)
            LogMessage($"{nameof(IsInIgnoredInventoryCell)}: {item?.BaseName} at cell x{column} y{row} ignored={ignored}", 5, Color.DarkGray);

        _lastInventoryScanStatus =
            $"{_lastScanItemCount} item(s), {_lastScanIgnoredCount} in ignored cells (last item: x{column} y{row})";
        return ignored;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        DrawIgnoredCellsSettings();
    }

    private void DrawIgnoredCellsSettings()
    {
        // Plain two-argument BeginChild on purpose: the flag enums moved around between the
        // ImGui.NET and Hexa.NET.ImGui bindings, this overload exists in both.
        ImGui.Separator();
        ImGui.BeginChild("##NinjaPriceIgnoredCellsMain", new Vector2(ImGui.GetContentRegionAvail().X, 230f));
        ImGui.Text("Ignored Inventory Cells (read-only - edit them in HighlightedItems)");
        ImGui.Text($"HighlightedItems: {_highlightedItemsStatus}");
        ImGui.Text($"Last inventory pass: {_lastInventoryScanStatus}");

        var contentRegionAvail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("##NinjaPriceIgnoredCellsGrid", new Vector2(contentRegionAvail.X, contentRegionAvail.Y));

        for (var row = 0; row < IgnoredCellRowCount; row++)
        {
            for (var column = 0; column < IgnoredCellColumnCount; column++)
            {
                // The checkbox only mirrors the file; clicking it changes nothing on purpose.
                var ignored = _ignoredCells[row, column];
                ImGui.Checkbox($"##NinjaPriceIgnoredCell{row}_{column}", ref ignored);

                if (column < IgnoredCellColumnCount - 1)
                    ImGui.SameLine();
            }
        }

        ImGui.EndChild();
        ImGui.EndChild();
    }
}
