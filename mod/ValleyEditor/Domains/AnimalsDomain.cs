using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.GameData.FarmAnimals;
using StardewValley.TokenizableStrings;
using ValleyEditor.Server;
using ValleyEditor.Sprites;

namespace ValleyEditor.Domains;

/// <summary>Farm animals: friendship, mood, age, name.</summary>
internal sealed class AnimalsDomain : Domain
{
    /// <summary>Friendship tops out at 1000 (5 hearts); mood and fullness at 255.</summary>
    private const int MaxFriendship = 1000;
    private const int MaxMood = 255;

    private readonly ItemSprites sprites;

    public AnimalsDomain(GameThreadDispatcher game, EditorState state, ItemSprites sprites)
        : base(game, state)
    {
        this.sprites = sprites;
    }

    public override void Register(Router router)
    {
        router.Get("/api/animals", _ => this.Read(Snapshot));

        router.Patch("/api/animals/{id}", request =>
        {
            JObject body = request.BodyObject;
            string? name = body.Value<string>("name")?.Trim();
            int? friendship = OptInt(body, "friendship", 0, MaxFriendship);
            int? happiness = OptInt(body, "happiness", 0, MaxMood);
            int? fullness = OptInt(body, "fullness", 0, MaxMood);
            if (name is { Length: 0 or > 64 })
                throw new ApiException(400, "'name' must be 1 to 64 characters.");

            return this.Write(() =>
            {
                FarmAnimal animal = Find(request.Params["id"]);
                if (name != null)
                {
                    animal.Name = name;
                    animal.displayName = name;
                }
                if (friendship.HasValue)
                    animal.friendshipTowardFarmer.Value = friendship.Value;
                if (happiness.HasValue)
                    animal.happiness.Value = happiness.Value;
                if (fullness.HasValue)
                    animal.fullness.Value = fullness.Value;
                return Snapshot();
            });
        });

        router.Post("/api/animals/{id}/grow", request => this.Write(() =>
        {
            Find(request.Params["id"]).growFully();
            return Snapshot();
        }));

        // max friendship, mood and fullness, petted today; optionally grow the babies too
        router.Post("/api/animals/pamper", request =>
        {
            bool grow = request.BodyObject.Value<bool?>("grow") ?? false;
            return this.Write(() =>
            {
                foreach (FarmAnimal animal in All().Select(a => a.Animal))
                {
                    animal.friendshipTowardFarmer.Value = MaxFriendship;
                    animal.happiness.Value = MaxMood;
                    animal.fullness.Value = MaxMood;
                    animal.wasPet.Value = true;
                    if (grow)
                        animal.growFully();
                }
                foreach (Pet pet in AllPets())
                {
                    PetToday(pet);
                    pet.friendshipTowardFarmer.Value = Pet.maxFriendship;
                    pet.GrantLoveMailIfNecessary();
                    if (pet.GetPetBowl() is { } bowl)
                        bowl.watered.Value = true;
                }
                return Snapshot();
            });
        });

        router.Get("/api/animal-sprites/{id}", async request =>
            new BinaryResult(await this.sprites.RenderPng(() => ItemSprites.AnimalPixels(Find(request.Params["id"]))), "image/png"));

        // pets (cat, dog, turtle...): an NPC subclass, not a FarmAnimal
        router.Get("/api/pets", _ => this.Read(PetsSnapshot));

        router.Patch("/api/pets/{id}", request =>
        {
            JObject body = request.BodyObject;
            string? name = body.Value<string>("name")?.Trim();
            int? friendship = OptInt(body, "friendship", 0, Pet.maxFriendship);
            if (name is { Length: 0 or > 64 })
                throw new ApiException(400, "'name' must be 1 to 64 characters.");

            return this.Write(() =>
            {
                Pet pet = FindPet(request.Params["id"]);
                if (name != null)
                    pet.Name = name;
                if (friendship.HasValue)
                {
                    pet.friendshipTowardFarmer.Value = friendship.Value;
                    pet.GrantLoveMailIfNecessary(); // the "loves you" message and Marnie's adoption letter, as at 1000
                }
                return PetsSnapshot();
            });
        });

        router.Post("/api/pets/{id}/pet", request => this.Write(() =>
        {
            PetToday(FindPet(request.Params["id"]));
            return PetsSnapshot();
        }));

        router.Post("/api/pets/{id}/water", request => this.Write(() =>
        {
            PetBowl bowl = FindPet(request.Params["id"]).GetPetBowl() ?? throw new ApiException(409, "This pet has no water bowl.");
            bowl.watered.Value = true;
            return PetsSnapshot();
        }));

        router.Get("/api/pet-sprites/{id}", async request => new BinaryResult(await this.sprites.RenderPng(() =>
        {
            FindPet(request.Params["id"]).GetPetIcon(out string asset, out Rectangle source);
            return ItemSprites.ReadPixels(Game1.content.Load<Texture2D>(asset), source);
        }), "image/png"));
    }

    private static List<Pet> AllPets()
    {
        var pets = new List<Pet>();
        Utility.ForEachLocation(location =>
        {
            pets.AddRange(location.characters.OfType<Pet>());
            return true;
        });
        return pets;
    }

    private static Pet FindPet(string id)
    {
        return AllPets().FirstOrDefault(p => p.petId.Value.ToString() == id)
            ?? throw new ApiException(404, $"No pet with ID '{id}'.");
    }

    private static bool PettedToday(Pet pet) => pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out int day) && day == Game1.Date.TotalDays;

    /// <summary>Pet it like the game does (Pet.checkAction): once a day, +12 friendship the first time.</summary>
    private static void PetToday(Pet pet)
    {
        if (PettedToday(pet))
            return;
        pet.lastPetDay[Game1.player.UniqueMultiplayerID] = Game1.Date.TotalDays;
        if (!pet.grantedFriendshipForPet.Value)
        {
            pet.grantedFriendshipForPet.Value = true;
            pet.friendshipTowardFarmer.Value = Math.Min(Pet.maxFriendship, pet.friendshipTowardFarmer.Value + 12);
            pet.timesPet.Value++;
        }
        pet.GrantLoveMailIfNecessary();
    }

    private static object PetsSnapshot()
    {
        return AllPets()
            .Select(pet =>
            {
                PetBowl? bowl = pet.GetPetBowl();
                return new
                {
                    Id = pet.petId.Value.ToString(),
                    pet.Name,
                    Type = pet.petType.Value,
                    TypeName = pet.GetPetData()?.DisplayName is { Length: > 0 } typeName ? TokenParser.ParseText(typeName) : pet.petType.Value,
                    LocationName = pet.currentLocation?.DisplayName ?? pet.currentLocation?.Name,
                    Friendship = pet.friendshipTowardFarmer.Value,
                    MaxFriendship = Pet.maxFriendship,
                    PettedToday = PettedToday(pet),
                    TimesPet = pet.timesPet.Value,
                    HasBowl = bowl != null,
                    BowlWatered = bowl?.watered.Value ?? false,
                };
            })
            .ToArray();
    }

    private static List<(GameLocation Location, FarmAnimal Animal)> All()
    {
        var all = new List<(GameLocation, FarmAnimal)>();
        Utility.ForEachLocation(location =>
        {
            foreach (FarmAnimal animal in location.animals.Values)
                all.Add((location, animal));
            return true;
        });
        return all;
    }

    private static FarmAnimal Find(string id)
    {
        return long.TryParse(id, out long animalId) && All().FirstOrDefault(a => a.Animal.myID.Value == animalId).Animal is { } animal
            ? animal
            : throw new ApiException(404, $"No animal with ID '{id}'.");
    }

    private static object Snapshot()
    {
        return All()
            .Select(a =>
            {
                FarmAnimal animal = a.Animal;
                FarmAnimalData? data = animal.GetAnimalData();
                string? produce = animal.currentProduce.Value;
                return new
                {
                    Id = animal.myID.Value.ToString(),
                    animal.Name,
                    Type = animal.type.Value,
                    TypeName = data?.DisplayName is { Length: > 0 } typeName ? TokenParser.ParseText(typeName) : animal.type.Value,
                    LocationName = a.Location.DisplayName ?? a.Location.Name,
                    Home = animal.home is { } home ? (home.GetData()?.Name is { Length: > 0 } homeName ? TokenParser.ParseText(homeName) : home.buildingType.Value) : null,
                    Friendship = animal.friendshipTowardFarmer.Value,
                    Happiness = animal.happiness.Value,
                    Fullness = animal.fullness.Value,
                    Age = animal.age.Value,
                    DaysToMature = data?.DaysToMature ?? 0,
                    IsAdult = animal.isAdult(),
                    WasPet = animal.wasPet.Value,
                    Produce = string.IsNullOrEmpty(produce) ? null : ItemRegistry.QualifyItemId(produce) ?? produce,
                    Mood = animal.getMoodMessage(),
                };
            })
            .OrderBy(a => a.Home, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
