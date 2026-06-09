using HarmonyLib;
using KMod;
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;

namespace CommandPallete
{
    public class CommandPallete_Patches : UserMod2
    {
        public static PAction TogglePaletteAction { get; private set; }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            Log.Debug("PUtil.InitLibrary() complete");

            new PPatchManager(harmony).RegisterPatchClass(typeof(CommandPallete_Patches));
            Log.Debug("Registered PPatchManager for CommandPallete_Patches (includes KScreenManager_OnKeyDown_Patch)");

            LocString.CreateLocStringKeys(typeof(CommandPalleteStrings));
            Localization.RegisterForTranslation(typeof(CommandPalleteStrings));
            Log.Debug("Registered LocStrings and localization for CommandPalleteStrings");

            TogglePaletteAction = new PActionManager().CreateAction(
                CommandPalleteStrings.ACTION_TOGGLE_PALETTE,
                CommandPalleteStrings.INPUT_BINDINGS.ROOT.TOGGLEPALETTE,
                new PKeyBinding(KKeyCode.P, Modifier.Ctrl | Modifier.Shift));
            Log.Debug("Created PAction '{0}' with default binding Ctrl+Shift+P, action.Value={1}"
                .F(CommandPalleteStrings.ACTION_TOGGLE_PALETTE, (int)TogglePaletteAction.GetKAction()));

            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            Log.Debug("Registered PLocalization and PVersionCheck");

            var opts = POptions.ReadSettings<CommandPalleteOptions>() ??
                new CommandPalleteOptions();
            Log.DebugEnabled = opts.EnableDebugLogging;
            Log.Debug("Options loaded: MaxResults={0}, FuzzyThreshold={1}, DebugLogging={2}"
                .F(opts.MaxResults, opts.FuzzyThreshold, opts.EnableDebugLogging));

            new POptions().RegisterOptions(this, typeof(CommandPalleteOptions));
            Log.Debug("OnLoad complete");
        }

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
            Log.Debug("OnAllModsLoaded: rebuilding command index");
            CommandIndex.Instance.RebuildIndex();
        }
    }

    [HarmonyPatch(typeof(KScreenManager), "OnKeyDown")]
    public static class KScreenManager_OnKeyDown_Patch
    {
        public static void Prefix(KScreenManager __instance, KButtonEvent e)
        {
            if (CommandPallete_Patches.TogglePaletteAction == null)
            {
                Log.Debug("KScreenManager_OnKeyDown: TogglePaletteAction is null, skipping");
                return;
            }

            if (e.Consumed)
            {
                Log.Debug("KScreenManager_OnKeyDown: event already consumed, skipping");
                return;
            }

            var action = CommandPallete_Patches.TogglePaletteAction.GetKAction();
            Log.Debug("KScreenManager_OnKeyDown: checking action={0} (int={1})"
                .F(action, (int)action));

            if (e.TryConsume(action))
            {
                Log.Debug("KScreenManager_OnKeyDown: action consumed! Toggling palette");
                CommandPalleteScreen.Toggle();
            }
            else
            {
                Log.Debug("KScreenManager_OnKeyDown: TryConsume returned false");
            }
        }
    }

    internal static class Log
    {
        internal static bool DebugEnabled { get; set; } = true;

        internal static void Debug(string message)
        {
            if (DebugEnabled)
                PUtil.LogDebug(message);
        }
    }
}
