using System;
using StardewModdingAPI;

namespace ValleyEditor.Integrations;

/// <summary>The part of Generic Mod Config Menu's API this mod uses (see GMCM's IGenericModConfigMenuApi).</summary>
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

    void AddParagraph(IManifest mod, Func<string> text);

    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
}

/// <summary>Adds the mod's settings to Generic Mod Config Menu, when it's installed.</summary>
internal static class GenericModConfigMenu
{
    public static void Register(IModHelper helper, IManifest manifest, Func<ModConfig> getConfig, Action<ModConfig> setConfig, Action onSaved)
    {
        IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        ITranslationHelper t = helper.Translation;
        api.Register(
            manifest,
            reset: () => setConfig(new ModConfig()),
            save: () =>
            {
                helper.WriteConfig(getConfig());
                onSaved();
            });

        api.AddParagraph(manifest, () => t.Get("config.intro"));
        api.AddNumberOption(
            manifest,
            getValue: () => getConfig().Port,
            setValue: value => getConfig().Port = value,
            name: () => t.Get("config.port"),
            tooltip: () => t.Get("config.port.tooltip"),
            min: 1024,
            max: 65535);
        api.AddBoolOption(
            manifest,
            getValue: () => getConfig().OpenBrowserOnSaveLoaded,
            setValue: value => getConfig().OpenBrowserOnSaveLoaded = value,
            name: () => t.Get("config.open-browser"),
            tooltip: () => t.Get("config.open-browser.tooltip"));
    }
}
