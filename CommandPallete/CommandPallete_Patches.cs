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
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new PPatchManager(harmony).RegisterPatchClass(typeof(CommandPallete_Patches));
            LocString.CreateLocStringKeys(typeof(CommandPalleteStrings));
            Localization.RegisterForTranslation(typeof(CommandPalleteStrings));
            new PActionManager().CreateAction(
                CommandPalleteStrings.ACTION_TOGGLE_PALETTE,
                CommandPalleteStrings.INPUT_BINDINGS.ROOT.TOGGLEPALETTE);
            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            var opts = POptions.ReadSettings<CommandPalleteOptions>() ??
                new CommandPalleteOptions();
            Log.DebugEnabled = opts.EnableDebugLogging;
            new POptions().RegisterOptions(this, typeof(CommandPalleteOptions));
            Log.Debug("OnLoad complete");
        }

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
            CommandIndex.Instance.RebuildIndex();
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
