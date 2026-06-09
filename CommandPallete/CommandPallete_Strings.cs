using PeterHan.PLib.Core;

namespace CommandPallete
{
    public static class CommandPalleteStrings
    {
        public const string ACTION_TOGGLE_PALETTE = "action_toggle_command_palette";

        public static class INPUT_BINDINGS
        {
            public static class ROOT
            {
                public static LocString TOGGLEPALETTE = "Toggle Command Palette";
            }
        }

        public static class UI
        {
            public static class COMMANDPALETTE
            {
                public static LocString NAME = "Command Palette";
                public static LocString DESCRIPTION = "Quick-search and execute commands, buildings, tools, and overlays.";
                public static LocString SEARCH_PLACEHOLDER = "Type a command...";
                public static LocString NO_RESULTS = "No matching commands found";
            }
        }

        public static class CATEGORIES
        {
            public static LocString BUILDING = "Building";
            public static LocString TOOL = "Tool";
            public static LocString OVERLAY = "Overlay";
            public static LocString SCREEN = "Screen";
            public static LocString ACTION = "Action";
        }
    }
}
