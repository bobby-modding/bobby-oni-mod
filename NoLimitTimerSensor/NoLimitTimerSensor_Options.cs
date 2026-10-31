using Newtonsoft.Json;
using PeterHan.PLib.Options;
using UnityEngine;
namespace NoLimitTimerSensor
{
	[ModInfo("https://github.com/bobby-modding/bobby-oni-mod")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class NoLimitTimerSensorOptions
	{

		[Option(
            "[Recommendation] Set the maximum timer under 60,000 seconds or 100 cycles.",
            "Due to 32-bit floating-point precision limits and repeated float addition, timers accumulate error over time and eventually freeze.\n\n" +
            "Simulated Drift & Error Rates Table:\n" +
			"Timer Show Counting from 0 to ... \tIn Game Actual Time Pass \tError Rate\n" +
            "600s (1 c)\t~600.1s\t+0.02%\n" +
            "6,000s (10 c)\t~6,000.9s\t+0.02%\n" +
            "30,000s (50 c)\t~30,095s\t+0.32%\n" +
            "60,000s (100 c)\t~58,694s\t-2.18%\n" +
            "1,000,000s (~1,667 c)\t~806,991s\t-19.3%\n" +
            "≥1,048,576s (≥1,748 c)\tFreezes\tStop Counting")]
        public LocText WarningText => null;
	[Option("Maximum Seconds", "The maximum on/off duration in seconds for the Timer Sensor.")]
	[JsonProperty]
	public float MaxSeconds {
		get => _maxSeconds;
		set => _maxSeconds = Mathf.Max(600f, value);
	}

	[Option("Maximum Cycles", "The maximum on/off duration in cycles for the Timer Sensor.")]
	[JsonProperty]
	public float MaxCycles {
		get => _maxCycles;
		set => _maxCycles = Mathf.Max(10f, value);
	}

	private float _maxSeconds;
	private float _maxCycles;

	public NoLimitTimerSensorOptions() {
		_maxSeconds = 600f;
		_maxCycles = 10f;
	}
	}
}
