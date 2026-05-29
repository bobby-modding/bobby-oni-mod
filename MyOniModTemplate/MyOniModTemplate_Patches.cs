using HarmonyLib;
using KMod;
using System.Collections.Generic;

namespace MyOniModTemplate
{
	public class MyOniModTemplate_Patches : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			// do some stuff before patching
			Debug.Log("[MyOniModTemplate] OnLoad: Before patches!");

			// let the game patch everything
			base.OnLoad(harmony);

			// do some stuff after patching
			Debug.Log("[MyOniModTemplate] OnLoad: After patches!");
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
		{
			foreach (var mod in mods)
				{
					Debug.Log($"[MyOniModTemplate] Found mod: {mod.title}, id: {mod.staticID}, version: {mod.packagedModInfo?.version}");
				}
		}
	}
}
