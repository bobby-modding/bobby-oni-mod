# No Limit Timer Sensor

Raises the Timer Sensor's duration limits. The stock sensor caps a timer at 600 seconds
(1 cycle) or 10 cycles; set the seconds or cycles past those limits in the sensor's side
screen and this mod lets the setting through instead of clamping it back.

## What it does

The game caps a timer when it sets the side screen's slider ranges — once when the mode is
toggled and again when the display is refreshed for a new target. Both places read the
sensor's own maxima (600 seconds, 10 cycles). The mod hooks both so those maxima come from
its options instead:

- **Maximum Seconds** — the cap when the sensor is set in seconds (default `600`).
- **Maximum Cycles** — the cap when the sensor is set in cycles (default `10`; one cycle is
  600 seconds of game time, and the display keeps converting between the two).

Timers you set past the stock limits are scheduled like any other timer — only the cap goes
away. The defaults are the stock values, so installing the mod changes nothing until you
raise them.

## Options

The game asks once when it starts and writes a settings file.

| Option | Default | Effect |
|---|---|---|
| Maximum Seconds | `600` | Cap applied to a seconds-based timer; lower values are ignored, so `600` is also the floor |
| Maximum Cycles | `10` | Cap applied to a cycles-based timer; lower values are ignored, so `10` is also the floor |

The options dialog also shows a recommendation, not an option you can change: keep the
maximum under 60,000 seconds or 100 cycles. Timer lengths are held as 32-bit floats and are
added up over time, so very long timers drift and eventually stop counting — the warning
prints a table of the observed error rates, from about +0.02% at 600 seconds to a frozen
counter at and above 1,048,576 seconds (~1,748 cycles).

## Mod info

| | |
|---|---|
| **Mod version** | 1.1.0 |
| **Requires game build** | U59-744825 or newer |
| **DLC / supported content** | ALL |
| **Static ID** | `nolimittimersensor` |
