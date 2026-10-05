using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.Tools;
using ValleyEditor.Server;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

internal static partial class ItemEditor
{
    /// <summary>Apply a details edit. Returns a new item when the change needs one (tool level, flavor, combined ring), else null.</summary>
    public static Item? Apply(Item item, JObject body)
    {
        Item? replacement = null;

        if (item is SObject obj && item is not Ring and not Furniture)
        {
            if (Domain.OptInt(body, "price", 0, 1_000_000_000) is int price)
                obj.Price = price;
            if (Domain.OptInt(body, "edibility", -300, 100_000) is int edibility)
                obj.Edibility = edibility;
            if (body.Value<string>("ingredient") is { Length: > 0 } ingredientId)
                replacement = Reflavor(obj, ingredientId);
        }

        if (item is MeleeWeapon weapon && body["weapon"] is JObject stats)
            ApplyWeaponStats(weapon, stats);

        if (item is Tool tool && body["enchantments"] is JObject enchantments)
        {
            foreach ((string id, JToken? value) in enchantments)
            {
                int level = value?.Type == JTokenType.Integer ? value.Value<int>() : throw new ApiException(400, $"Enchantment '{id}' needs an integer level.");
                SetEnchantment(tool, id, Math.Clamp(level, 0, MaxEnchantLevel));
            }
        }

        if (item is Tool tool2 and not MeleeWeapon && body.Value<string>("toolLevel") is { Length: > 0 } toolId && toolId != tool2.QualifiedItemId)
            replacement = Upgrade(tool2, toolId);

        if (item is WateringCan can && body.Value<bool?>("fillWater") == true)
            can.WaterLeft = can.waterCanMax;

        if (item is Ring ring && body.Value<string>("combineWith") is { Length: > 0 } ringId)
        {
            Ring other = ItemRegistry.Create(ringId, allowNull: true) as Ring ?? throw new ApiException(400, $"'{ringId}' isn't a ring.");
            replacement = ring.Combine(other);
        }

        if (item is Boots boots)
        {
            if (Domain.OptInt(body, "defense", 0, 10_000) is int defense)
                boots.defenseBonus.Value = defense;
            if (Domain.OptInt(body, "immunity", 0, 10_000) is int immunity)
                boots.immunityBonus.Value = immunity;
        }

        if (item is Clothing clothing && body.Value<string>("color") is { } hex)
        {
            clothing.clothesColor.Value = ParseColor(hex);
            clothing.isPrismatic.Value = false;
        }

        if (replacement != null)
        {
            replacement.Stack = item.Stack;
            if (ItemJson.CanHaveQuality(replacement))
                replacement.Quality = item.Quality;
        }
        return replacement;
    }

    private static void ApplyWeaponStats(MeleeWeapon weapon, JObject stats)
    {
        if (Domain.OptInt(stats, "minDamage", 0, 1_000_000) is int min)
            weapon.minDamage.Value = min;
        if (Domain.OptInt(stats, "maxDamage", 0, 1_000_000) is int max)
            weapon.maxDamage.Value = max;
        if (weapon.maxDamage.Value < weapon.minDamage.Value)
            weapon.maxDamage.Value = weapon.minDamage.Value;
        if (Domain.OptInt(stats, "speed", -100, 100) is int speed)
            weapon.speed.Value = speed;
        if (Domain.OptInt(stats, "precision", -1000, 1000) is int precision)
            weapon.addedPrecision.Value = precision;
        if (Domain.OptInt(stats, "defense", -1000, 10_000) is int defense)
            weapon.addedDefense.Value = defense;
        if (Domain.OptInt(stats, "areaOfEffect", -100, 1000) is int aoe)
            weapon.addedAreaOfEffect.Value = aoe;
        if (OptFloat(stats, "knockback", 0, 100) is float knockback)
            weapon.knockback.Value = knockback;
        if (OptFloat(stats, "critChance", 0, 1) is float critChance)
            weapon.critChance.Value = critChance;
        if (OptFloat(stats, "critMultiplier", 0, 1000) is float critMultiplier)
            weapon.critMultiplier.Value = critMultiplier;
    }

    /// <summary>Add, remove or re-level an enchantment, past the game's own limits (one prismatic enchantment, 3 forges...).</summary>
    private static void SetEnchantment(Tool tool, string id, int level)
    {
        BaseEnchantment template = EnchantmentFactories.Select(create => create()).FirstOrDefault(e => EnchantmentId(e) == id)
            ?? throw new ApiException(400, $"Unknown enchantment '{id}'.");
        BaseEnchantment? current = tool.enchantments.FirstOrDefault(e => e.GetType() == template.GetType());
        bool leveled = Group(template) != "primary";

        if (level == 0)
        {
            if (current != null)
                tool.RemoveEnchantment(current);
            return;
        }
        if (current is null)
        {
            if (leveled)
                template.Level = level;
            tool.enchantments.Add(template);
            template.ApplyTo(tool, tool.lastUser);
        }
        else if (leveled && current.GetLevel() != level)
        {
            // what BaseEnchantment.SetLevel does, without its level cap
            current.UnapplyTo(tool, tool.lastUser);
            current.Level = level;
            current.ApplyTo(tool, tool.lastUser);
        }
    }

    /// <summary>Swap a tool for another level of the same kind (Data/Tools ClassName), keeping enchantments and attachments.</summary>
    private static Tool Upgrade(Tool old, string newId)
    {
        Tool tool = ItemRegistry.Create(newId, allowNull: true) as Tool ?? throw new ApiException(400, $"'{newId}' isn't a tool.");
        if (tool.GetToolData()?.ClassName != old.GetToolData()?.ClassName)
            throw new ApiException(400, $"'{newId}' isn't a level of '{old.QualifiedItemId}'.");
        tool.UpgradeFrom(old);
        for (int i = 0; i < Math.Min(old.AttachmentSlotsCount, tool.AttachmentSlotsCount); i++)
            tool.attachments[i] = old.attachments[i];
        if (tool is WateringCan can)
            can.WaterLeft = can.waterCanMax;
        return tool;
    }

    /// <summary>Remake a flavored item (wine, jelly, roe...) from another ingredient, the way machines create them.</summary>
    private static Item Reflavor(SObject obj, string ingredientId)
    {
        if (obj.preserve.Value is not SObject.PreserveType preserve)
            throw new ApiException(409, "This item has no ingredient.");
        SObject ingredient = ItemRegistry.Create(ingredientId, allowNull: true) as SObject ?? throw new ApiException(400, $"'{ingredientId}' isn't an object.");
        return ItemRegistry.GetObjectTypeDefinition().CreateFlavoredItem(preserve, ingredient)
            ?? throw new ApiException(409, $"The game can't make this item from '{ingredientId}'.");
    }

    private static float? OptFloat(JObject body, string name, float min, float max)
    {
        JToken? token = body[name];
        if (token is null || token.Type == JTokenType.Null)
            return null;
        if (token.Type is not (JTokenType.Float or JTokenType.Integer))
            throw new ApiException(400, $"'{name}' must be a number.");
        float value = token.Value<float>();
        return value >= min && value <= max ? value : throw new ApiException(400, $"'{name}' must be between {min} and {max}.");
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            throw new ApiException(400, "'color' must look like #rrggbb.");
        return new Color((int)(rgb >> 16) & 0xFF, (int)(rgb >> 8) & 0xFF, (int)rgb & 0xFF);
    }
}
