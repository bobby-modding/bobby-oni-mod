using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System.Collections.Generic;

namespace NoLimitTimerSensor
{
	public class NoLimitTimerSensor_Patches : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(NoLimitTimerSensorOptions));
		}


		internal static NoLimitTimerSensorOptions GetOptions()
		{
			return POptions.ReadSettings<NoLimitTimerSensorOptions>() ??
				new NoLimitTimerSensorOptions();
		}
	}

	[HarmonyPatch(typeof(TimerSideScreen), "UpdateVisualsForNewTarget")]
	internal static class TimerSideScreen_UpdateVisualsForNewTarget_Patch
	{
		internal static void Prefix(ref float ___maxSeconds, ref float ___maxCycles)
		{
			var options = NoLimitTimerSensor_Patches.GetOptions();
			___maxSeconds = options.MaxSeconds;
			___maxCycles = options.MaxCycles;
		}
	}

	[HarmonyPatch(typeof(TimerSideScreen), "ToggleMode")]
	internal static class TimerSideScreen_ToggleMode_Patch
	{
		internal static void Prefix(ref float ___maxSeconds, ref float ___maxCycles)
		{
			var options = NoLimitTimerSensor_Patches.GetOptions();
			___maxSeconds = options.MaxSeconds;
			___maxCycles = options.MaxCycles;
		}
	}
}
