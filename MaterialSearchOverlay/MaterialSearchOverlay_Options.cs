using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace BobbyModding.MaterialSearchOverlay {
    [ModInfo("https://github.com/bobby-modding/bobby-oni-mod")]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MaterialSearchOverlayOptions {
        [Option("Enable Debug Logging", "Logs detailed debug information to Player.log")]
        [JsonProperty]
        public bool EnableDebugLogging { get; set; }

        [Option("Show Type Badges", "Shows what kind of thing each search result is, next to its name")]
        [JsonProperty]
        public bool ShowTypeBadges { get; set; }

        public MaterialSearchOverlayOptions() {
            EnableDebugLogging = false;
            ShowTypeBadges = true;
        }
    }
}
