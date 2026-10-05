using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using StardewValley;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.GameData.WorldMaps;
using StardewValley.Network;
using StardewValley.TokenizableStrings;
using ValleyEditor.Sprites;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Date, time, weather and teleporting. Mirrors the game's own debug commands (DebugCommands.cs).</summary>
internal sealed class WorldDomain : Domain
{
    private const string DefaultContext = "Default";
    private static readonly string[] Weathers = { Game1.weather_sunny, Game1.weather_rain, Game1.weather_lightning, Game1.weather_snow, Game1.weather_debris, Game1.weather_green_rain };

    private readonly ItemSprites sprites;

    public WorldDomain(GameThreadDispatcher game, EditorState state, ItemSprites sprites)
        : base(game, state)
    {
        this.sprites = sprites;
    }

    public override void Register(Router router)
    {
        router.Get("/api/world", _ => this.Read(Snapshot));

        router.Patch("/api/world", request =>
        {
            JObject body = request.BodyObject;
            int? day = OptInt(body, "day", 1, 28);
            int? year = OptInt(body, "year", 1, 999);
            int? time = OptInt(body, "time", 600, 2550);
            Season? season = null;
            if (body.Value<string>("season") is { } seasonName)
                season = Enum.TryParse(seasonName, ignoreCase: true, out Season parsed) && Enum.IsDefined(parsed)
                    ? parsed
                    : throw new ApiException(400, "'season' must be one of: spring, summer, fall, winter.");
            if (time.HasValue && (time.Value % 100 >= 60 || time.Value % 10 != 0))
                throw new ApiException(400, "'time' uses the game's HHMM format in 10-minute steps, e.g. 630 or 1450.");

            return this.Write(() =>
            {
                int oldYear = Game1.year, oldDay = Game1.dayOfMonth, oldTime = Game1.timeOfDay;
                Season oldSeason = Game1.season;
                uint oldDaysPlayed = Game1.stats.DaysPlayed;
                UndoCapture.Remember(() =>
                {
                    Game1.year = oldYear;
                    if (Game1.season != oldSeason)
                    {
                        Game1.season = oldSeason;
                        Game1.setGraphicsForSeason();
                    }
                    Game1.dayOfMonth = oldDay;
                    Game1.stats.DaysPlayed = oldDaysPlayed;
                    Game1.timeOfDay = oldTime;
                });

                if (year.HasValue)
                    Game1.year = year.Value;
                if (season.HasValue && season.Value != Game1.season)
                {
                    Game1.season = season.Value;
                    Game1.setGraphicsForSeason();
                }
                if (day.HasValue)
                    Game1.dayOfMonth = day.Value;
                if (day.HasValue || season.HasValue || year.HasValue)
                    Game1.stats.DaysPlayed = (uint)(Game1.seasonIndex * 28 + Game1.dayOfMonth + (Game1.year - 1) * 4 * 28);
                if (time.HasValue)
                {
                    Game1.timeOfDay = time.Value;
                    Game1.outdoorLight = Color.White;
                }
                return Snapshot();
            });
        });

        router.Put("/api/world/weather/{context}", request =>
        {
            string context = request.Params["context"];
            JObject body = request.BodyObject;
            string? today = ParseWeather(body, "today");
            string? tomorrow = ParseWeather(body, "tomorrow");

            return this.Write(() =>
            {
                if (!Game1.netWorldState.Value.LocationWeather.ContainsKey(context))
                    throw new ApiException(404, $"Unknown location context '{context}'.");
                LocationWeather weather = Game1.netWorldState.Value.GetWeatherForLocation(context);

                if (today != null)
                    SetToday(context, weather, today);
                if (tomorrow != null)
                {
                    weather.WeatherForTomorrow = tomorrow;
                    if (context == DefaultContext)
                        Game1.netWorldState.Value.WeatherForTomorrow = Game1.weatherForTomorrow = tomorrow;
                }
                return Snapshot();
            });
        });

        // the world map (Data/WorldMap): regions with clickable areas linked to locations
        router.Get("/api/world/map", _ => this.Read(MapRegions));
        router.Get("/api/world/map/{region}/image", async request =>
        {
            string region = request.Params["region"];
            byte[] png = await this.sprites.RenderPng(() => ComposeMap(region));
            return new BinaryResult(png, "image/png");
        });

        router.Post("/api/world/warp", request =>
        {
            string name = request.BodyObject.Value<string>("location") ?? throw new ApiException(400, "'location' is required.");
            return this.Write(() =>
            {
                GameLocation location = Game1.getLocationFromName(name) ?? throw new ApiException(404, $"Unknown location '{name}'.");
                var (x, y) = ArrivalTile(location);
                Game1.warpFarmer(new LocationRequest(location.NameOrUniqueName, location.uniqueName.Value != null, location), x, y, 2);
                return Snapshot();
            });
        });
    }

    /// <summary>
    /// Where to land in a location: the game's default warp point when it has one, else where the doors and warps
    /// leading here arrive (e.g. the cellar stairs), else the free tile nearest the middle of the map.
    /// </summary>
    private static (int X, int Y) ArrivalTile(GameLocation location)
    {
        bool Standable(int x, int y) => location.isTileOnMap(x, y) && location.CanSpawnCharacterHere(new Vector2(x, y));

        int dx = -1, dy = -1;
        Utility.getDefaultWarpLocation(location.Name, ref dx, ref dy);
        if (Standable(dx, dy))
            return (dx, dy);

        Point? fromWarp = null;
        Utility.ForEachLocation(other =>
        {
            Warp? warp = other.warps.FirstOrDefault(w => w.TargetName == location.NameOrUniqueName || w.TargetName == location.Name);
            if (warp != null)
                fromWarp = new Point(warp.TargetX, warp.TargetY);
            return fromWarp is null;
        });
        if (fromWarp is { } p && location.isTileOnMap(p.X, p.Y))
            return (p.X, p.Y);

        Point size = new(location.Map.Layers[0].LayerWidth, location.Map.Layers[0].LayerHeight);
        for (int radius = 0; radius < Math.Max(size.X, size.Y); radius++)
        {
            for (int x = size.X / 2 - radius; x <= size.X / 2 + radius; x++)
            {
                for (int y = size.Y / 2 - radius; y <= size.Y / 2 + radius; y++)
                {
                    if (Standable(x, y))
                        return (x, y);
                }
            }
        }
        return (Math.Max(dx, 0), Math.Max(dy, 0));
    }

    private static object MapRegions()
    {
        string? current = Game1.player.currentLocation?.Name;
        return DataLoader.WorldMap(Game1.content)
            .Select(pair =>
            {
                Rectangle bounds = MapBounds(pair.Value);
                return new
                {
                    Id = pair.Key,
                    bounds.Width,
                    bounds.Height,
                    // one clickable spot per tooltip (an area like Town has one per building), biggest first so smaller ones sit on top
                    Areas = pair.Value.MapAreas
                        .Where(area => GameStateQuery.CheckConditions(area.Condition))
                        .SelectMany(area => AreaTooltips(area)
                            .Select(tooltip =>
                            {
                                Rectangle hit = !tooltip.PixelArea.IsEmpty ? tooltip.PixelArea : area.PixelArea;
                                string? location = TooltipLocation(area, tooltip, hit);
                                string text = TokenParser.ParseText(tooltip.Text) is { Length: > 0 } parsed
                                    ? parsed
                                    : Game1.getLocationFromName(location ?? "")?.DisplayName ?? "";
                                string[] lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                return new
                                {
                                    Id = $"{area.Id}/{tooltip.Id}",
                                    Name = lines.FirstOrDefault(),
                                    Detail = lines.Length > 1 ? string.Join(" · ", lines.Skip(1)) : null,
                                    hit.X,
                                    hit.Y,
                                    hit.Width,
                                    hit.Height,
                                    Location = location,
                                    Current = location != null && location == current,
                                };
                            }))
                        .Where(a => a.Width > 0 && a.Height > 0)
                        .OrderByDescending(a => a.Width * a.Height)
                        .ToArray(),
                };
            })
            .ToArray();
    }

    /// <summary>
    /// Guess which location a map tooltip stands for. Tooltips have no location of their own, so pick the area's
    /// world position whose pixel area best overlaps the tooltip; ties favour a location named like the tooltip or
    /// area, then outdoor locations (the 'Town' tooltip covers the whole area, like every building's interior does).
    /// </summary>
    /// <summary>An area's visible tooltips, or one covering the whole area if it has none, so every area stays clickable.</summary>
    private static IEnumerable<WorldMapTooltipData> AreaTooltips(WorldMapAreaData area)
    {
        List<WorldMapTooltipData> tooltips = area.Tooltips.Where(t => GameStateQuery.CheckConditions(t.Condition)).ToList();
        if (tooltips.Count == 0)
            tooltips.Add(new WorldMapTooltipData { Id = area.Id, PixelArea = area.PixelArea });
        return tooltips;
    }

    private static string? TooltipLocation(WorldMapAreaData area, WorldMapTooltipData tooltip, Rectangle hit)
    {
        return area.WorldPositions
            .Where(p => GameStateQuery.CheckConditions(p.Condition))
            .SelectMany(p =>
            {
                Rectangle spot = !p.MapPixelArea.IsEmpty ? p.MapPixelArea : area.PixelArea;
                double overlap = Math.Round(Overlap(hit, spot), 2); // near-equal rectangles tie, so the name rules decide
                return p.LocationNames.Prepend(p.LocationName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => (Name: n, Overlap: overlap));
            })
            .Select(c => (c.Name, c.Overlap, Location: Game1.getLocationFromName(c.Name)))
            // no overlap still counts, ranked last: some areas (the desert) place their positions apart from their tooltips
            .Where(c => c.Location != null)
            .OrderByDescending(c => c.Overlap)
            .ThenByDescending(c => c.Name.Equals(tooltip.Id, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Name.Equals(area.Id, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Location!.IsOutdoors)
            .Select(c => c.Name)
            .FirstOrDefault();
    }

    /// <summary>Intersection over union of two rectangles (1 = identical, 0 = disjoint).</summary>
    private static double Overlap(Rectangle a, Rectangle b)
    {
        Rectangle inter = Rectangle.Intersect(a, b);
        if (inter.IsEmpty)
            return 0;
        double i = inter.Width * inter.Height;
        return i / (a.Width * a.Height + b.Width * b.Height - i);
    }

    /// <summary>The map size: the union of the base textures' areas.</summary>
    private static Rectangle MapBounds(WorldMapRegionData region)
    {
        Rectangle bounds = Rectangle.Empty;
        foreach (WorldMapTextureData texture in region.BaseTexture.Where(t => GameStateQuery.CheckConditions(t.Condition)))
        {
            Rectangle area = texture.MapPixelArea;
            if (area.IsEmpty)
            {
                Rectangle source = texture.SourceRect;
                if (source.IsEmpty)
                    source = Game1.content.Load<Texture2D>(texture.Texture).Bounds;
                area = new Rectangle(0, 0, source.Width, source.Height);
            }
            bounds = bounds.IsEmpty ? area : Rectangle.Union(bounds, area);
        }
        return bounds;
    }

    /// <summary>Draw a region like the game's map page: base textures, then the area textures whose conditions pass (restored buildings, etc.).</summary>
    private static (Color[] Pixels, int Width, int Height) ComposeMap(string regionId)
    {
        if (!DataLoader.WorldMap(Game1.content).TryGetValue(regionId, out WorldMapRegionData? region))
            throw new ApiException(404, $"No world map region '{regionId}'.");

        Rectangle bounds = MapBounds(region);
        var canvas = new Color[bounds.Width * bounds.Height];

        foreach (WorldMapTextureData texture in region.BaseTexture.Where(t => GameStateQuery.CheckConditions(t.Condition)))
            Draw(canvas, bounds, texture, Rectangle.Empty);
        foreach (WorldMapAreaData area in region.MapAreas.Where(a => GameStateQuery.CheckConditions(a.Condition)))
        {
            foreach (WorldMapTextureData texture in area.Textures.Where(t => GameStateQuery.CheckConditions(t.Condition)))
                Draw(canvas, bounds, texture, area.PixelArea);
        }
        return (canvas, bounds.Width, bounds.Height);
    }

    /// <summary>Alpha-blend a map texture onto the canvas (premultiplied, like game textures), scaling with nearest-neighbour if needed.</summary>
    private static void Draw(Color[] canvas, Rectangle bounds, WorldMapTextureData data, Rectangle defaultArea)
    {
        Texture2D texture = Game1.content.Load<Texture2D>(data.Texture);
        Rectangle source = data.SourceRect.IsEmpty ? texture.Bounds : data.SourceRect;
        Rectangle dest = !data.MapPixelArea.IsEmpty ? data.MapPixelArea : !defaultArea.IsEmpty ? defaultArea : new Rectangle(0, 0, source.Width, source.Height);
        var (pixels, width, height) = ItemSprites.ReadPixels(texture, source);

        for (int y = 0; y < dest.Height; y++)
        {
            int cy = dest.Y + y - bounds.Y;
            if (cy < 0 || cy >= bounds.Height)
                continue;
            int sy = y * height / dest.Height;
            for (int x = 0; x < dest.Width; x++)
            {
                int cx = dest.X + x - bounds.X;
                if (cx < 0 || cx >= bounds.Width)
                    continue;
                Color src = pixels[sy * width + x * width / dest.Width];
                if (src.A == 0)
                    continue;
                ref Color dst = ref canvas[cy * bounds.Width + cx];
                float keep = 1 - src.A / 255f;
                dst = new Color(
                    (byte)Math.Min(255, src.R + dst.R * keep),
                    (byte)Math.Min(255, src.G + dst.G * keep),
                    (byte)Math.Min(255, src.B + dst.B * keep),
                    (byte)Math.Min(255, src.A + dst.A * keep));
            }
        }
    }

    /// <summary>Change today's weather flags, as at the start of a day (Game1.ApplyWeatherForNewDay).</summary>
    private static void SetToday(string context, LocationWeather weather, string value)
    {
        weather.Weather = value;
        weather.IsRaining = value is Game1.weather_rain or Game1.weather_lightning or Game1.weather_green_rain;
        weather.IsLightning = value == Game1.weather_lightning;
        weather.IsGreenRain = value == Game1.weather_green_rain;
        weather.IsSnowing = value == Game1.weather_snow;
        weather.IsDebrisWeather = value == Game1.weather_debris;

        if (context != DefaultContext)
            return;
        Game1.isRaining = weather.IsRaining;
        Game1.isLightning = weather.IsLightning;
        Game1.isGreenRain = weather.IsGreenRain;
        Game1.isSnowing = weather.IsSnowing;
        Game1.isDebrisWeather = weather.IsDebrisWeather;
        if (Game1.isDebrisWeather)
            Game1.populateDebrisWeatherArray();
        Game1.updateWeatherIcon();
    }

    private static string? ParseWeather(JObject body, string field)
    {
        string? value = body.Value<string>(field);
        if (value is null)
            return null;
        return Weathers.FirstOrDefault(w => w.Equals(value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiException(400, $"'{field}' must be one of: {string.Join(", ", Weathers)}.");
    }

    private static object Snapshot()
    {
        return new
        {
            Day = Game1.dayOfMonth,
            Season = Game1.season.ToString().ToLowerInvariant(),
            Game1.year,
            Time = Game1.timeOfDay,
            Game1.stats.DaysPlayed,
            Weathers,
            Weather = Game1.netWorldState.Value.LocationWeather.Pairs
                .OrderBy(p => p.Key == DefaultContext ? 0 : 1)
                .ThenBy(p => p.Key)
                .Select(p => new { Context = p.Key, Today = p.Value.Weather, Tomorrow = p.Value.WeatherForTomorrow }),
            CurrentLocation = Game1.player.currentLocation?.Name,
            Locations = Game1.locations
                .Select(l => new { l.Name, DisplayName = l.DisplayName ?? l.Name })
                .OrderBy(l => l.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };
    }
}
