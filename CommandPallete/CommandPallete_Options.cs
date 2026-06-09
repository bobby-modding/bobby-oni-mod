using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace CommandPallete
{
    [ModInfo("https://github.com/bobby-modding/bobby-oni-mod")]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CommandPalleteOptions
    {
        [Option("Max Results", "Maximum number of search results to display")]
        [JsonProperty]
        public int MaxResults { get; set; }

        [Option("Fuzzy Match Threshold", "Minimum similarity score (0-100) for fuzzy matching")]
        [JsonProperty]
        public int FuzzyThreshold { get; set; }

        [Option("Enable Debug Logging", "Logs detailed debug information to Player.log")]
        [JsonProperty]
        public bool EnableDebugLogging { get; set; }

        public CommandPalleteOptions()
        {
            MaxResults = 10;
            FuzzyThreshold = 40;
            EnableDebugLogging = false;
        }
    }
}
