using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Tools;
using StardewValley.GameData.Weapons;
using StardewValley.Objects;
using StardewValley.Tools;
using ValleyEditor.Server;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

/// <summary>Type-specific item editing: object price, weapon stats, enchantments and forges, tool level, rings, boots, clothing.</summary>
internal static partial class ItemEditor
{
    /// <summary>The highest enchantment level the editor allows (the game caps most at 3 forges or a few innate levels).</summary>
    private const int MaxEnchantLevel = 99;

    /// <summary>Every enchantment the editor offers, keyed by its class name minus "Enchantment".</summary>
    private static readonly Func<BaseEnchantment>[] EnchantmentFactories =
    {
        // forges
        () => new RubyEnchantment(), () => new EmeraldEnchantment(), () => new AquamarineEnchantment(),
        () => new JadeEnchantment(), () => new AmethystEnchantment(), () => new TopazEnchantment(),
        () => new GalaxySoulEnchantment(),
        // innate weapon enchantments
        () => new AttackEnchantment(), () => new CritEnchantment(), () => new CritPowerEnchantment(),
        () => new DefenseEnchantment(), () => new LightweightEnchantment(), () => new WeaponSpeedEnchantment(),
        () => new SlimeGathererEnchantment(), () => new SlimeSlayerEnchantment(),
        // prismatic shard enchantments
        () => new ArtfulEnchantment(), () => new BugKillerEnchantment(), () => new VampiricEnchantment(),
        () => new CrusaderEnchantment(), () => new HaymakerEnchantment(), () => new PowerfulEnchantment(),
        () => new ReachingToolEnchantment(), () => new ShavingEnchantment(), () => new BottomlessEnchantment(),
        () => new GenerousEnchantment(), () => new ArchaeologistEnchantment(), () => new MasterEnchantment(),
        () => new AutoHookEnchantment(), () => new PreservingEnchantment(), () => new EfficientToolEnchantment(),
        () => new SwiftToolEnchantment(), () => new FisherEnchantment(),
    };

    private static string EnchantmentId(BaseEnchantment e) => e.GetType().Name.Replace("Enchantment", "");

    private static string Group(BaseEnchantment e) => e.IsForge() ? "forge" : e.IsSecondaryEnchantment() ? "innate" : "primary";

    public static object Describe(Item item)
    {
        return new
        {
            Kind = item switch
            {
                MeleeWeapon => "weapon",
                Tool => "tool",
                Ring => "ring",
                Boots => "boots",
                Clothing => "clothing",
                SObject => "object",
                _ => "other",
            },
            Object = item is SObject obj && item is not Ring and not Furniture ? DescribeObject(obj) : null,
            Weapon = item is MeleeWeapon weapon ? DescribeWeapon(weapon) : null,
            Enchantments = item is Tool tool ? DescribeEnchantments(tool) : null,
            Tool = item is Tool t2 and not MeleeWeapon ? DescribeTool(t2) : null,
            Ring = item is Ring ring ? DescribeRing(ring) : null,
            Boots = item is Boots boots ? new { Defense = boots.defenseBonus.Value, Immunity = boots.immunityBonus.Value } : null,
            Clothing = item is Clothing clothing ? new { Color = $"#{clothing.clothesColor.Value.R:x2}{clothing.clothesColor.Value.G:x2}{clothing.clothesColor.Value.B:x2}", Dyeable = clothing.dyeable.Value } : null,
        };
    }

    private static object DescribeObject(SObject obj)
    {
        ObjectData? data = ItemRegistry.GetData(obj.QualifiedItemId)?.RawData as ObjectData;
        string? ingredient = obj.preservedParentSheetIndex.Value;
        return new
        {
            obj.Price,
            BasePrice = obj.preserve.Value.HasValue ? (int?)null : data?.Price,
            obj.Edibility,
            BaseEdibility = data?.Edibility,
            Preserve = obj.preserve.Value?.ToString(),
            Ingredient = string.IsNullOrEmpty(ingredient) ? null : ItemRegistry.QualifyItemId(ingredient) ?? ingredient,
        };
    }

    private static object DescribeWeapon(MeleeWeapon weapon)
    {
        WeaponData? data = weapon.GetData();
        return new
        {
            MinDamage = weapon.minDamage.Value,
            MaxDamage = weapon.maxDamage.Value,
            Speed = weapon.speed.Value,
            Precision = weapon.addedPrecision.Value,
            Defense = weapon.addedDefense.Value,
            AreaOfEffect = weapon.addedAreaOfEffect.Value,
            Knockback = weapon.knockback.Value,
            CritChance = weapon.critChance.Value,
            CritMultiplier = weapon.critMultiplier.Value,
            Base = data is null ? null : new
            {
                data.MinDamage, data.MaxDamage, data.Speed, data.Precision, data.Defense, data.AreaOfEffect,
                data.Knockback, data.CritChance, data.CritMultiplier,
            },
        };
    }

    private static object[] DescribeEnchantments(Tool tool)
    {
        return EnchantmentFactories
            .Select(create => create())
            .Where(e => e.CanApplyTo(tool) || tool.enchantments.Any(c => c.GetType() == e.GetType()))
            .Select(e =>
            {
                BaseEnchantment? current = tool.enchantments.FirstOrDefault(c => c.GetType() == e.GetType());
                string group = Group(e);
                return (object)new
                {
                    Id = EnchantmentId(e),
                    Name = e.GetDisplayName(),
                    Group = group,
                    Leveled = group != "primary",
                    Level = current is null ? 0 : Math.Max(1, current.GetLevel()),
                    GameMaxLevel = e.GetMaximumLevel(),
                };
            })
            .ToArray();
    }

    private static object? DescribeTool(Tool tool)
    {
        ToolData? data = tool.GetToolData();
        var levels = data?.ClassName is { } className
            ? DataLoader.Tools(Game1.content)
                .Where(pair => pair.Value.ClassName == className)
                .OrderBy(pair => pair.Value.UpgradeLevel)
                .Select(pair => new { Id = "(T)" + pair.Key, Name = ItemRegistry.GetDataOrErrorItem("(T)" + pair.Key).DisplayName, pair.Value.UpgradeLevel })
                .ToArray()
            : null;
        return new
        {
            Current = tool.QualifiedItemId,
            Levels = levels,
            WaterLeft = (tool as WateringCan)?.WaterLeft,
            WaterMax = (tool as WateringCan)?.waterCanMax,
        };
    }

    private static object DescribeRing(Ring ring)
    {
        return new
        {
            Combined = ring is CombinedRing combined ? combined.combinedRings.Select(r => r.QualifiedItemId).ToArray() : Array.Empty<string>(),
            Rings = ItemRegistry.GetObjectTypeDefinition().GetAllIds()
                .Select(id => ItemRegistry.GetDataOrErrorItem("(O)" + id))
                .Where(d => d.Category == SObject.ringCategory)
                .Select(d => new { Id = d.QualifiedItemId, Name = d.DisplayName })
                .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
        };
    }
}
