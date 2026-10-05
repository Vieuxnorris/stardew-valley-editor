using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using StardewValley;
using ValleyEditor.Rules;
using ValleyEditor.Server;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

/// <summary>Placed machines (kegs, furnaces...): finish now, change the output, and per-machine speed.</summary>
internal sealed class MachinesDomain : Domain
{
    private readonly RulesService rules;

    public MachinesDomain(GameThreadDispatcher game, EditorState state, RulesService rules)
        : base(game, state)
    {
        this.rules = rules;
    }

    public override void Register(Router router)
    {
        router.Get("/api/machines/{id}", request => this.Read(() => Describe(Find(request.Params["id"]))));

        router.Post("/api/machines/{id}/finish", request => this.Write(() =>
        {
            SObject machine = Find(request.Params["id"]);
            RememberMachine(machine);
            if (!MachineTools.Finish(machine))
                throw new ApiException(409, "This machine isn't working on anything.");
            return Describe(machine);
        }));

        // finish every working machine in a location, or everywhere when 'location' is omitted
        router.Post("/api/machines/finish-all", request =>
        {
            string? location = (request.Body as JObject)?.Value<string>("location");
            return this.Write(() =>
            {
                int finished = 0;
                Utility.ForEachLocation(l =>
                {
                    if (location is null || l.NameOrUniqueName == location)
                        finished += l.objects.Values.Count(o => MachineTools.IsMachine(o) && MachineTools.Finish(o));
                    return true;
                });
                return new { Finished = finished };
            });
        });

        // replace what the machine is making (null empties it)
        router.Put("/api/machines/{id}/output", request =>
        {
            JObject body = request.BodyObject;
            string? itemId = body.Value<string>("qualifiedId");
            int stack = OptInt(body, "stack", 1, 999_999) ?? 1;
            int quality = OptInt(body, "quality", 0, 4) ?? 0;
            return this.Write(() =>
            {
                SObject machine = Find(request.Params["id"]);
                RememberMachine(machine);
                if (string.IsNullOrEmpty(itemId))
                {
                    machine.heldObject.Value = null;
                    machine.readyForHarvest.Value = false;
                    machine.MinutesUntilReady = 0;
                    machine.showNextIndex.Value = false;
                    return Describe(machine);
                }

                SObject output = ItemRegistry.Create(itemId, stack, quality, allowNull: true) as SObject
                    ?? throw new ApiException(400, $"'{itemId}' isn't an object a machine can hold.");
                machine.heldObject.Value = output;
                if (!machine.readyForHarvest.Value && machine.MinutesUntilReady <= 0)
                    MachineTools.Finish(machine); // an idle machine shows its new output right away
                return Describe(machine);
            });
        });

        // the time multiplier for every machine of this type (0 = instant, null = back to the global rule)
        router.Put("/api/machines/types/{machine}/speed", request =>
        {
            JToken? token = request.BodyObject["multiplier"];
            double? multiplier = token is null || token.Type == JTokenType.Null
                ? null
                : token.Type is JTokenType.Float or JTokenType.Integer && token.Value<double>() is >= 0 and <= 10
                    ? token.Value<double>()
                    : throw new ApiException(400, "'multiplier' must be between 0 (instant) and 10, or null.");
            string machineId = request.Params["machine"];
            return this.Write(() =>
            {
                this.rules.Update(r =>
                {
                    if (multiplier is double value)
                        r.MachineTimeOverrides[machineId] = value;
                    else
                        r.MachineTimeOverrides.Remove(machineId);
                });
                return new { Multiplier = RulesService.Current.MachineTimeFor(machineId), Custom = RulesService.Current.MachineTimeOverrides.ContainsKey(machineId) };
            });
        });
    }

    /// <summary>Let undo put the machine's output and timer back.</summary>
    private static void RememberMachine(SObject machine)
    {
        SObject? output = machine.heldObject.Value is { } held ? (SObject)ItemCloner.Clone(held) : null;
        int minutes = machine.MinutesUntilReady;
        bool ready = machine.readyForHarvest.Value, nextIndex = machine.showNextIndex.Value;
        UndoCapture.Remember(() =>
        {
            machine.heldObject.Value = output;
            machine.MinutesUntilReady = minutes;
            machine.readyForHarvest.Value = ready;
            machine.showNextIndex.Value = nextIndex;
        });
    }

    /// <summary>A machine's ID: <c>location@x,y</c>, like chests.</summary>
    private static SObject Find(string id)
    {
        int at = id.LastIndexOf('@');
        string[] xy = at < 0 ? Array.Empty<string>() : id[(at + 1)..].Split(',');
        if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y))
            throw new ApiException(400, "A machine ID looks like 'Location@x,y'.");
        string locationName = id[..at];

        SObject? found = null;
        Utility.ForEachLocation(l =>
        {
            if (l.NameOrUniqueName == locationName && l.objects.TryGetValue(new Vector2(x, y), out SObject obj) && MachineTools.IsMachine(obj))
                found = obj;
            return found is null;
        });
        return found ?? throw new ApiException(404, $"No machine at '{id}'. It may have been moved; refresh the map.");
    }

    public static object Describe(SObject machine)
    {
        SObject? output = machine.heldObject.Value;
        Item? input = machine.lastInputItem.Value;
        RulesData r = RulesService.Current;
        return new
        {
            QualifiedId = machine.QualifiedItemId,
            Name = machine.DisplayName,
            Output = output is null ? null : new { QualifiedId = output.QualifiedItemId, Name = output.DisplayName, output.Stack, output.Quality },
            Input = input is null ? null : new { QualifiedId = input.QualifiedItemId, Name = input.DisplayName },
            Ready = machine.readyForHarvest.Value,
            Working = MachineTools.IsWorking(machine),
            MinutesUntilReady = Math.Max(0, machine.MinutesUntilReady),
            Speed = r.MachineTimeFor(machine.QualifiedItemId),
            CustomSpeed = r.MachineTimeOverrides.ContainsKey(machine.QualifiedItemId),
            GlobalSpeed = r.MachineTime,
        };
    }
}
