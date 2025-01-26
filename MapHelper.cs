using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory;
// using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.FilesInMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Models;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using Base = ExileCore2.PoEMemory.Components.Base;
using Map = ExileCore2.PoEMemory.Components.Map;
using Mods = ExileCore2.PoEMemory.Components.Mods;
using RectangleF = ExileCore2.Shared.RectangleF;

namespace MapHelper;

public class MapHelper : BaseSettingsPlugin<MapHelperSettings>
{
    private bool IsHovering()
    {
        try
        {
            var uiHover = GameController.Game.IngameState.UIHover;
            if (uiHover.AsObject<HoverItemIcon>().ToolTipType != ToolTipType.ItemInChat)
            {
                var inventoryItemIcon = uiHover.AsObject<NormalInventoryItem>();
                var tooltip = inventoryItemIcon.Tooltip;
                var poeEntity = inventoryItemIcon.Item;
                if (tooltip != null && poeEntity.Address != 0 && poeEntity.IsValid)
                {
                    var item = inventoryItemIcon.Item;
                    var baseItemType = GameController.Files.BaseItemTypes.Translate(item.Path);
                    if (baseItemType != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
            // ignored
            //LogError("Error in GetHoveredItem()", 10);
        }
    }

    private IngameState InGameState => GameController.IngameState;
    private List<string> BannedModifiers;
    private List<string> TerribleModifiers;

    private void ParseBannedModifiers()
    {
        BannedModifiers = Settings
            .Score.BannedModifiers.Value.Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        TerribleModifiers = Settings
            .Score.TerribleModifiers.Value.Split(',')
            .Select(x => x.Trim().ToLower())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public override bool Initialise()
    {
        //BannedModifiers = ParseBannedModifiers();
        Settings.Score.ReloadModifiers.OnPressed = ParseBannedModifiers;
        ParseBannedModifiers();
        return base.Initialise();
    }

    public override void Render()
    {
        IList<WaystoneItem> waystones = [];

        var stashPanel = InGameState.IngameUi.StashElement;
        var stashPanelGuild = InGameState.IngameUi.GuildStashElement;
        var inventoryPanel = InGameState.IngameUi.InventoryPanel;

        bool isQuadTab = false;

        // Run if inventory panel is opened
        if (inventoryPanel.IsVisible)
        {
            // Add stash items
            if (stashPanel.IsVisible && stashPanel.VisibleStash != null)
            {
                if (stashPanel.VisibleStash.TotalBoxesInInventoryRow == 24)
                {
                    isQuadTab = true;
                }
                foreach (var item in stashPanel.VisibleStash.VisibleInventoryItems)
                {
                    waystones.Add(
                        new WaystoneItem(
                            item.Item.GetComponent<Base>(),
                            item.Item.GetComponent<Map>(),
                            item.Item.GetComponent<Mods>(),
                            item.GetClientRectCache,
                            ItemLocation.Stash
                        )
                    );
                }
            }
            else if (stashPanelGuild.IsVisible && stashPanelGuild != null)
            {
                if (stashPanelGuild.VisibleStash.TotalBoxesInInventoryRow == 24)
                {
                    isQuadTab = true;
                }
                foreach (var item in stashPanelGuild.VisibleStash.VisibleInventoryItems)
                {
                    waystones.Add(
                        new WaystoneItem(
                            item.Item.GetComponent<Base>(),
                            item.Item.GetComponent<Map>(),
                            item.Item.GetComponent<Mods>(),
                            item.GetClientRectCache,
                            ItemLocation.Stash
                        )
                    );
                }
            }
            // Add inventory items
            var inventoryItems = GameController
                .IngameState
                .ServerData
                .PlayerInventories[0]
                .Inventory
                .InventorySlotItems;
            foreach (var item in inventoryItems)
            {
                waystones.Add(
                    new(
                        item.Item.GetComponent<Base>(),
                        item.Item.GetComponent<Map>(),
                        item.Item.GetComponent<Mods>(),
                        item.GetClientRect(),
                        ItemLocation.Inventory
                    )
                );
            }

            foreach (var waystone in waystones)
            {
                var item = waystone.map;

                if (item == null)
                    continue;

                // Check for map tier
                if (item.Tier < Settings.Score.MinimumTier)
                {
                    continue;
                }

                var itemMods = waystone.mods;
                var rarity = itemMods.ItemRarity.ToString();

                var bbox = waystone.rect;
                var middlePoint = bbox.Width / 2;

                var goodSize = (float)Math.Floor((float)(bbox.Height * .5));
                // var goodOffset = middlePoint - goodSize;
                var goodBbox = new ExileCore2.Shared.RectangleF(
                    bbox.X + 2,
                    bbox.Y + 2,
                    middlePoint,
                    goodSize
                );

                var badSize = (float)Math.Floor((float)(bbox.Height * .5));
                // var badOffset = middlePoint - badSize;
                var badBbox = new ExileCore2.Shared.RectangleF(
                    bbox.X + middlePoint,
                    bbox.Y + 2,
                    middlePoint - 2,
                    badSize
                );

                if (rarity == "Normal")
                {
                    DrawBorderHighlight(
                        bbox,
                        Settings.Graphics.NormalHighlightColor,
                        Settings.Graphics.BorderHighlight.TypeBorderThickness
                    );
                    // continue;
                }

                if (rarity == "Magic")
                {
                    DrawBorderHighlight(
                        bbox,
                        Settings.Graphics.MagicHighlightColor,
                        Settings.Graphics.BorderHighlight.TypeBorderThickness
                    );
                    // continue;
                }

                int prefixCount = 0;
                int suffixCount = 0;

                int score = 0;
                int badScore = 0;

                int iiq = 0;
                int iir = 0;
                int iid = 0;
                bool extraRareMod = false;
                int packSize = 0;
                int magicPackSize = 0;
                int extraPacks = 0;
                int extraMagicPack = 0;
                int extraRarePack = 0;
                int additionalPacks = 0;

                var drawColor = Color.White;
                bool hasBannedMod = false;
                bool hasTerribleMod = false;
                bool isCorrupted = waystone.baseComponent?.isCorrupted ?? false;
                int deliriousLeft = isCorrupted ? 0 : 3;

                if (isCorrupted)
                {
                    DrawBorderHighlight(
                        bbox,
                        Settings.Graphics.CorruptedHighlightColor,
                        Settings.Graphics.BorderHighlight.TypeBorderThickness
                    );
                    // continue;
                }

                // Iterate through the mods
                foreach (var mod in itemMods.ItemMods)
                {
                    // Check for banned modifiers
                    if (BannedModifiers.Count > 0)
                    {
                        foreach (var bannedMod in BannedModifiers)
                        {
                            if (
                                mod.DisplayName.Contains(
                                    bannedMod,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                badScore += 1;
                                hasBannedMod = true;
                                // break;
                            }
                        }
                    }

                    // Check for terrible modifiers
                    if (TerribleModifiers.Count > 0)
                    {
                        foreach (var terribleMod in TerribleModifiers)
                        {
                            if (
                                mod.DisplayName.Contains(
                                    terribleMod,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                badScore += 5;
                                hasTerribleMod = true;
                                // break;
                            }
                        }
                    }

                    // Count prefixes and suffixes
                    if (mod.DisplayName.StartsWith("of", StringComparison.OrdinalIgnoreCase))
                    {
                        suffixCount++;
                    }
                    else
                    {
                        if (mod.Group != "AfflictionMapDeliriumStacks")
                        {
                            prefixCount++;
                        }
                    }

                    // Find good mods
                    switch (mod.Name)
                    {
                        case "MapDroppedItemRarityIncrease":
                            iir += mod.Values[0];
                            break;
                        case "MapDroppedItemQuantityIncrease":
                            iiq += mod.Values[0];
                            if (mod.Values.Count != 1)
                            {
                                iir += mod.Values[1];
                            }
                            break;
                        case "InstilledMapDelirium":
                            iid += mod.Values[0];
                            if (!isCorrupted)
                            {
                                deliriousLeft--;
                            }
                            break;
                        case "MapRareMonstersAdditionalModifier":
                            extraRareMod = true;
                            break;
                        case "MapPackSizeIncrease":
                            packSize += mod.Values[0];
                            break;
                        case "MapMagicPackSizeIncrease":
                            magicPackSize += mod.Values[0];
                            break;
                        case "MapTotalEffectivenessIncrease":
                            extraPacks += mod.Values[0];
                            break;
                        case "MapMagicPackIncrease":
                            extraMagicPack += mod.Values[0];
                            break;
                        case "MapMagicRarePackIncrease":
                            extraRarePack += mod.Values[0];
                            if (mod.Values.Count != 1)
                            {
                                extraMagicPack += mod.Values[1];
                            }
                            break;
                        case "MapRarePackIncrease":
                            extraRarePack += mod.Values[0];
                            break;
                        case string s when s.StartsWith("MapMonsterAdditionalPacks"):
                            additionalPacks += mod.Values[0];
                            break;
                    }
                }

                // Sum the score
                score += iiq * Settings.Score.ScorePerQuantity;
                score += iir * Settings.Score.ScorePerRarity;
                score += iid * Settings.Score.ScorePerDelirious;
                score += packSize * Settings.Score.ScorePerPackSize;
                score += magicPackSize * Settings.Score.ScorePerMagicPackSize;
                score += extraPacks * Settings.Score.ScorePerExtraPacksPercent;
                score += extraMagicPack * Settings.Score.ScorePerExtraMagicPack;
                score += extraRarePack * Settings.Score.ScorePerExtraRarePack;
                score += additionalPacks * Settings.Score.ScorePerAdditionalPack;
                if (extraRareMod)
                {
                    score += Settings.Score.ScoreForExtraRareMonsterModifier;
                }

                // Drawing

                // Frame

                // else
                // {
                if (score >= Settings.Score.MinimumCraftHighlightScore)
                {
                    if (score >= Settings.Score.MinimumRunHighlightScore)
                    {
                        switch (Settings.Graphics.RunHightlightStyle)
                        {
                            case 1:
                                DrawBorderHighlight(
                                    goodBbox,
                                    Settings.Graphics.RunHighlightColor,
                                    Settings.Graphics.BorderHighlight.RunBorderThickness.Value
                                );
                                break;
                            case 2:
                                DrawBoxHighlight(
                                    goodBbox,
                                    Settings.Graphics.RunHighlightColor,
                                    Settings.Graphics.BoxHighlight.RunBoxRounding.Value
                                );
                                break;
                        }
                    }
                    else if (prefixCount < 3 && !isCorrupted)
                    {
                        switch (Settings.Graphics.CraftHightlightStyle)
                        {
                            case 1:
                                DrawBorderHighlight(
                                    goodBbox,
                                    Settings.Graphics.CraftHighlightColor,
                                    Settings.Graphics.BorderHighlight.CraftBorderThickness.Value
                                );
                                break;
                            case 2:
                                DrawBoxHighlight(
                                    goodBbox,
                                    Settings.Graphics.CraftHighlightColor,
                                    Settings.Graphics.BoxHighlight.CraftBoxRounding.Value
                                );
                                break;
                        }
                    }
                }

                if (hasBannedMod || hasTerribleMod)
                {
                    Color badColor =
                        badScore >= Settings.Score.BadThresholdHighlightScore
                            ? Settings.Graphics.TerribleHighlightColor
                            : Settings.Graphics.BannedHighlightColor;

                    switch (Settings.Graphics.BannedHightlightStyle)
                    {
                        case 1:
                            DrawBorderHighlight(
                                badBbox,
                                badColor,
                                Settings.Graphics.BorderHighlight.BannedBorderThickness
                            );
                            break;
                        case 2:
                            DrawBoxHighlight(
                                badBbox,
                                badColor,
                                Settings.Graphics.BoxHighlight.BannedBoxRounding.Value
                            );
                            break;
                    }
                }
                // }

                if (
                    (
                        waystone.location == ItemLocation.Inventory
                        || (waystone.location == ItemLocation.Stash && !isQuadTab)
                    )
                    && Settings.Graphics.FontSize.EnableText
                    && !IsHovering()
                )
                {
                    // Stats
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    // using (Graphics.SetTextScale(Settings.Graphics.FontSize.QRFontSizeMultiplier))
                    // {
                    //     Graphics.DrawText(
                    //         iir.ToString(),
                    //         new Vector2(bbox.Left + 5, bbox.Top),
                    //         ExileCore2.Shared.Enums.FontAlign.Left
                    //     );
                    //     Graphics.DrawText(
                    //         iiq.ToString(),
                    //         new Vector2(
                    //             bbox.Left + 5,
                    //             bbox.Top
                    //                 + 2
                    //                 + (10 * Settings.Graphics.FontSize.QRFontSizeMultiplier)
                    //         ),
                    //         ExileCore2.Shared.Enums.FontAlign.Left
                    //     );
                    //     if (extraRareMod)
                    //     {
                    //         Graphics.DrawText(
                    //             "+1",
                    //             new Vector2(
                    //                 bbox.Left + 5,
                    //                 bbox.Top
                    //                     + 4
                    //                     + (20 * Settings.Graphics.FontSize.QRFontSizeMultiplier)
                    //             ),
                    //             ExileCore2.Shared.Enums.FontAlign.Left
                    //         );
                    //     }
                    // }

                    // Affixes count
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    // using (
                    //     Graphics.SetTextScale(Settings.Graphics.FontSize.PrefSuffFontSizeMultiplier)
                    // )
                    // {
                    //     Graphics.DrawText(
                    //         prefixCount.ToString(),
                    //         new Vector2(bbox.Right - 5, bbox.Top),
                    //         ExileCore2.Shared.Enums.FontAlign.Right
                    //     );
                    //     Graphics.DrawText(
                    //         suffixCount.ToString(),
                    //         new Vector2(
                    //             bbox.Right - 5,
                    //             bbox.Top
                    //                 + 2
                    //                 + (10 * Settings.Graphics.FontSize.PrefSuffFontSizeMultiplier)
                    //         ),
                    //         ExileCore2.Shared.Enums.FontAlign.Right
                    //     );
                    // }

                    // Score
                    // SetTextScale doesn't scale well we need to change origin point or add x:y placement modifications depending on scale
                    using (
                        Graphics.SetTextScale(Settings.Graphics.FontSize.ScoreFontSizeMultiplier)
                    )
                    {
                        if (deliriousLeft > 0)
                        {
                            Graphics.DrawText(
                                // score.ToString(),
                                deliriousLeft.ToString(),
                                new Vector2(
                                    bbox.Left + 5,
                                    bbox.Bottom
                                        - 20
                                        - (10 * Settings.Graphics.FontSize.ScoreFontSizeMultiplier)
                                ),
                                ExileCore2.Shared.Enums.FontAlign.Left
                            );
                        }
                        Graphics.DrawText(
                            // score.ToString(),
                            iid.ToString(),
                            new Vector2(
                                bbox.Left + 5,
                                bbox.Bottom
                                    - 7
                                    - (10 * Settings.Graphics.FontSize.ScoreFontSizeMultiplier)
                            ),
                            ExileCore2.Shared.Enums.FontAlign.Left
                        );
                    }
                }
            }
        }
    }

    private void DrawBorderHighlight(RectangleF rect, Color color, int thickness)
    {
        var isHovering = IsHovering();

        Color originalColor = color;
        Color adjustedColor = Color.FromArgb(20, originalColor.R, originalColor.G, originalColor.B);

        Color selectedColor = isHovering ? adjustedColor : originalColor;

        int scale = thickness - 1;
        int innerX = (int)rect.X + 1 + (int)(0.5 * scale);
        int innerY = (int)rect.Y + 1 + (int)(0.5 * scale);
        int innerWidth = (int)rect.Width - 1 - scale;
        int innerHeight = (int)rect.Height - 1 - scale;
        RectangleF scaledFrame = new RectangleF(innerX, innerY, innerWidth, innerHeight);
        Graphics.DrawFrame(scaledFrame, selectedColor, thickness);
    }

    private void DrawBoxHighlight(RectangleF rect, Color color, int rounding)
    {
        var isHovering = IsHovering();

        Color originalColor = color;
        Color adjustedColor = Color.FromArgb(20, originalColor.R, originalColor.G, originalColor.B);

        Color selectedColor = isHovering ? adjustedColor : originalColor;

        int innerX = (int)rect.X + 1 + (int)(0.5 * rounding);
        int innerY = (int)rect.Y + 1 + (int)(0.5 * rounding);
        int innerWidth = (int)rect.Width - 1 - rounding;
        int innerHeight = (int)rect.Height - 1 - rounding;
        RectangleF scaledBox = new RectangleF(innerX, innerY, innerWidth, innerHeight);
        Graphics.DrawBox(scaledBox, selectedColor, rounding);
    }
}
