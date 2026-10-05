using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Every player chest and fridge in the world, editable like the backpack.</summary>
internal sealed class ChestsDomain : Domain
{
    /// <summary>The tile part of a fridge's ID (fridges belong to the house, not a tile).</summary>
    private const string FridgeKey = "fridge";

    public ChestsDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/chests", _ => this.Read(List));
        router.Get("/api/chests/{chest}", request => this.Read(() => Snapshot(request.Params["chest"])));

        SlotRoutes.Register(router, "/api/chests/{chest}", this.Write, request => Container(Find(request.Params["chest"])), request => Snapshot(request.Params["chest"]));
    }

    /// <summary>A chest's ID: <c>location@x,y</c>, or <c>location@fridge</c>.</summary>
    private static IEnumerable<(string Id, GameLocation Location, Chest Chest, Vector2? Tile)> AllChests()
    {
        var found = new List<(string, GameLocation, Chest, Vector2?)>();
        Utility.ForEachLocation(location =>
        {
            string name = location.NameOrUniqueName;
            Chest? fridge = location switch
            {
                FarmHouse house => house.fridge.Value,
                IslandFarmHouse island => island.fridge.Value,
                _ => null,
            };
            if (fridge != null)
                found.Add(($"{name}@{FridgeKey}", location, fridge, null));

            foreach ((Vector2 tile, StardewValley.Object obj) in location.objects.Pairs)
            {
                if (obj is Chest chest && chest.playerChest.Value && chest.SpecialChestType is not (Chest.SpecialChestTypes.AutoLoader or Chest.SpecialChestTypes.Enricher))
                    found.Add(($"{name}@{(int)tile.X},{(int)tile.Y}", location, chest, tile));
            }
            return true;
        });
        return found;
    }

    private static object List()
    {
        return AllChests()
            .Select(c =>
            {
                IList<Item> items = c.Chest.GetItemsForPlayer();
                return new
                {
                    c.Id,
                    Name = c.Chest.DisplayName,
                    IsFridge = !c.Tile.HasValue,
                    QualifiedId = c.Chest.QualifiedItemId,
                    Location = c.Location.NameOrUniqueName,
                    LocationName = c.Location.DisplayName ?? c.Location.Name,
                    X = (int?)c.Tile?.X,
                    Y = (int?)c.Tile?.Y,
                    Used = items.Count(i => i != null),
                    Capacity = c.Chest.GetActualCapacity(),
                    Color = c.Chest.playerChoiceColor.Value is { A: > 0 } color && color != Color.Black ? $"#{color.R:x2}{color.G:x2}{color.B:x2}" : null,
                };
            })
            .OrderBy(c => c.LocationName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.Y)
            .ThenBy(c => c.X)
            .ToArray();
    }

    private static Chest Find(string id)
    {
        return AllChests().FirstOrDefault(c => c.Id == id).Chest
            ?? throw new ApiException(404, $"No chest '{id}'. It may have been moved or picked up; reload the list.");
    }

    private static ItemContainer Container(Chest chest)
    {
        return new ItemContainer(chest.GetItemsForPlayer(), chest.GetActualCapacity(), item => chest.addItem(item) == null);
    }

    private static object Snapshot(string id)
    {
        Chest chest = Find(id);
        ItemContainer container = Container(chest);
        return new { Size = container.Capacity, Slots = container.Slots() };
    }
}
