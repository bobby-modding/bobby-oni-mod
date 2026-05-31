using PeterHan.PLib.Core;

namespace BobbyModding.MaterialSearchOverlay {
    public static class MaterialSearchOverlayStrings {
        public const string OVERLAY_ACTION = "action_overlay_material_search";
        public const string OVERLAY_ICON = "overlay_material_search";

        public static class INPUT_BINDINGS {
            public static class ROOT {
                public static LocString MATERIALSEARCH = "Open Material Search Overlay";
            }
        }

        public static class UI {
            public static class OVERLAYS {
                public static class MATERIALSEARCH {
                    public static LocString NAME = "MATERIAL SEARCH OVERLAY";
                    public static LocString DESCRIPTION = "Searches for materials by name and highlights their locations on the map.";
                    public static LocString BUTTON = "Material Search Overlay";
                    public static LocString TOOLTIP = "Search for materials by typing their name";
                    public static LocString SEARCH_PLACEHOLDER = "Type material name...";

                    public static LocString MASS_LABEL = "Natural Tile: {0} |  Debris: {1} |  Buildings: {2}";

                    public static class TOOLTIPS {
                        public static LocString MATCH = "Material matches the search term";
                        public static LocString NO_MATCH = "Material does not match the search term";
                    }
                }
            }
        }
    }
}
