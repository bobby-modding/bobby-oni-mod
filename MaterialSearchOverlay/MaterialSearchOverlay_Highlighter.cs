using System.Collections.Generic;
using UnityEngine;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// A search term resolved into concrete match sets. Built either from the raw search
    /// text or from a single dropdown selection, then handed to
    /// <see cref="SearchHighlighter"/> which turns it into cells and entity targets.
    /// </summary>
    internal sealed class SearchMatches {
        /// <summary>Elements matched by name; drives both the cell overlay and backwalls.</summary>
        internal readonly HashSet<SimHashes> Elements = new HashSet<SimHashes>();

        /// <summary>
        /// Prefab tags for equipment, food, plants, industrial products and artifacts, kept
        /// per kind rather than in one flat set. A generic sweep over loose items has to work
        /// out what an entity actually is before it can report a count for it, and a single
        /// flattened set would lose that.
        /// </summary>
        private readonly Dictionary<TargetKind, HashSet<Tag>> prefabs =
            new Dictionary<TargetKind, HashSet<Tag>>();

        /// <summary>True when at least one prefab backed kind matched.</summary>
        internal bool HasPrefabMatches => prefabs.Count > 0;

        /// <summary>
        /// The kind a prefab tag was matched as, or null if the tag was not matched. Ordered
        /// most specific first: a fertilizer is both a food and an industrial product, and
        /// reporting it as food is the more useful answer.
        /// </summary>
        internal TargetKind? PrefabKind(Tag prefabTag) {
            if (prefabTag == null || !prefabTag.IsValid)
                return null;
            if (HasPrefab(TargetKind.Equipment, prefabTag))
                return TargetKind.Equipment;
            if (HasPrefab(TargetKind.Artifact, prefabTag))
                return TargetKind.Artifact;
            if (HasPrefab(TargetKind.Plant, prefabTag))
                return TargetKind.Plant;
            if (HasPrefab(TargetKind.Food, prefabTag))
                return TargetKind.Food;
            if (HasPrefab(TargetKind.Industrial, prefabTag))
                return TargetKind.Industrial;
            return null;
        }

        /// <summary>True when <paramref name="prefabTag"/> was matched as this kind.</summary>
        internal bool HasPrefab(TargetKind kind, Tag prefabTag) {
            return prefabs.TryGetValue(kind, out var tags) && tags.Contains(prefabTag);
        }

        /// <summary>Creature species tags, matched against CreatureBrain.species.</summary>
        internal readonly HashSet<Tag> Species = new HashSet<Tag>();

        /// <summary>Bionic upgrade prefab tags; matched against installed and loose upgrades.</summary>
        internal readonly HashSet<Tag> Boosters = new HashSet<Tag>();

        /// <summary>True when the query resolved the bionic duplicant model.</summary>
        internal bool MatchBionics;

        /// <summary>True when the query resolved the non bionic duplicant model.</summary>
        internal bool MatchDupes;

        /// <summary>
        /// True when the query resolved the general artifact row rather than one named piece.
        /// Kept apart from <see cref="HasPrefab(TargetKind, Tag)"/> because the category row
        /// has no prefab tag of its own to match against.
        /// </summary>
        internal bool MatchAllArtifacts;

        /// <summary>True when the match set needs entity highlighting at all.</summary>
        internal bool HasEntityMatches => HasPrefabMatches || Species.Count > 0 ||
            Boosters.Count > 0 || MatchBionics || MatchDupes || MatchAllArtifacts;

        /// <summary>
        /// True when nothing matched at all, so the overlay can clear itself instead of
        /// leaving the previous highlight up.
        /// </summary>
        internal bool IsEmpty => Elements.Count == 0 && !HasEntityMatches;

        internal static SearchMatches FromQuery(string upperQuery) {
            var matches = new SearchMatches();
            if (string.IsNullOrEmpty(upperQuery))
                return matches;
            var entries = SearchIndex.Match(upperQuery);
            for (int i = 0; i < entries.Count; i++)
                matches.Include(entries[i]);
            return matches;
        }

        internal static SearchMatches FromSelection(SearchEntry entry) {
            var matches = new SearchMatches();
            if (entry != null)
                matches.Include(entry);
            return matches;
        }

        private void Include(SearchEntry entry) {
            if (entry == null)
                return;
            switch (entry.Kind) {
            case TargetKind.Material:
                Elements.Add(entry.Element);
                break;
            case TargetKind.Critter:
                Species.Add(entry.Tag);
                break;
            case TargetKind.Booster:
                Boosters.Add(entry.Tag);
                break;
            case TargetKind.Bionic:
                MatchBionics = true;
                break;
            case TargetKind.Duplicant:
                MatchDupes = true;
                break;
            case TargetKind.Artifact:
                // The general row is the only artifact entry that carries no prefab tag.
                if (entry.Tag == null)
                    MatchAllArtifacts = true;
                else
                    AddPrefab(TargetKind.Artifact, entry.Tag);
                break;
            default:
                AddPrefab(entry.Kind, entry.Tag);
                break;
            }
        }

        private void AddPrefab(TargetKind kind, Tag prefabTag) {
            if (!prefabs.TryGetValue(kind, out var tags)) {
                tags = new HashSet<Tag>();
                prefabs[kind] = tags;
            }
            tags.Add(prefabTag);
        }
    }

    /// <summary>
    /// Turns a <see cref="SearchMatches"/> into the set of entities that should glow, plus a
    /// per entity highlight colour.
    /// <para>
    /// This runs once per search or selection, not per frame: the returned objects are held
    /// onto and followed wherever they move afterwards, so anything spawned later shows up
    /// only after the search runs again.
    /// </para>
    /// <para>
    /// Every collector here is main thread only. The cell overlay reads its own snapshot
    /// array from the SimDebugView worker thread and must never reach anything below.
    /// </para>
    /// </summary>
    internal static class SearchHighlighter {
        /// <summary>
        /// Matched entity count per kind, for the info label. Keyed by GameObject rather than
        /// by component instance: an entity is routinely reachable through several of its own
        /// components (a plant is a Uprootable, a Harvestable and a Pickupable; a piece of
        /// equipment is both an Equippable and a Pickupable), and a per component set counted
        /// a single object two or three times.
        /// </summary>
        private static readonly Dictionary<TargetKind, HashSet<GameObject>> counted =
            new Dictionary<TargetKind, HashSet<GameObject>>();

        /// <summary>Counts of matched entities by kind; zero when the kind did not match.</summary>
        internal static int CountFor(TargetKind kind) {
            return counted.TryGetValue(kind, out var set) ? set.Count : 0;
        }

        /// <summary>
        /// Clears the highlight colour of everything found last time and empties the target
        /// list, so a narrower search does not leave stale entities lit up.
        /// </summary>
        internal static void Reset(List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            for (int i = 0; i < targets.Count; i++) {
                var go = targets[i]?.gameObject;
                if (go == null)
                    continue;
                var anim = go.GetComponent<KBatchedAnimController>();
                if (anim != null) {
                    anim.HighlightColour = Color.clear;
                    anim.SetLayer(0);
                }
            }
            targets.Clear();
            colors.Clear();
            foreach (var set in counted.Values)
                set.Clear();
        }

        /// <summary>
        /// Adds every entity matching <paramref name="matches"/> to the highlight sets. Safe to
        /// call repeatedly with the same matches to pick up entities that spawned since.
        /// </summary>
        internal static void Collect(SearchMatches matches, List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            if (matches == null)
                return;
            // Ordered most specific first. Add() keeps the first colour written for a given
            // entity, so the specific collectors get to claim their entities before the
            // generic prefab scans at the end.
            if (matches.Elements.Count > 0)
                CollectElementEntities(matches, targets, colors);
            if (matches.HasPrefabMatches) {
                CollectArtifacts(matches, targets, colors);
                CollectPlantsAndFood(matches, targets, colors);
                CollectEquipables(matches, targets, colors);
                CollectBuildings(matches, targets, colors);
                CollectStoredItems(matches, targets, colors);
            }
            if (matches.Species.Count > 0)
                CollectCreatures(matches, targets, colors);
            if (matches.MatchAllArtifacts)
                CollectAnyArtifacts(targets, colors);
            CollectDuplicants(matches, targets, colors);
            if (matches.HasPrefabMatches)
                CollectPickupables(matches, targets, colors);
        }

        /// <summary>
        /// True when the entity sits in the active world and in explored space. Fog of war is
        /// honoured so the overlay does not reveal hidden colony contents.
        /// </summary>
        internal static bool IsRevealed(KMonoBehaviour cmp) {
            if (cmp == null)
                return false;
            int cell = Grid.PosToCell(cmp);
            return Grid.IsValidCell(cell) && Grid.IsActiveWorld(cell) &&
                Grid.Visible[cell] > 20;
        }

        /// <summary>
        /// Registers an entity, keyed by its GameObject so that two collectors reaching the
        /// same object through different components still produce one entry and one count.
        /// <para>
        /// The colour is stored against the GameObject rather than the component that happened
        /// to match first, because the game asks for the colour of whichever component it is
        /// currently drawing. Keying on the component made a match silently fail to light up
        /// whenever the renderer offered a different one.
        /// </para>
        /// <para>
        /// <paramref name="count"/> is false for entities that are only there to be lit up,
        /// so that pointing at a container or a loose item cannot inflate the per kind tally.
        /// </para>
        /// </summary>
        private static void Add(TargetKind kind, KMonoBehaviour target,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors,
                bool count = true, Color? swatch = null) {
            if (target == null)
                return;
            var go = target.gameObject;
            if (go == null)
                return;
            if (count) {
                if (!counted.TryGetValue(kind, out var set)) {
                    set = new HashSet<GameObject>();
                    counted[kind] = set;
                }
                set.Add(go);
            }
            if (colors.ContainsKey(go))
                return;
            // A kind only has one colour, so anything matched as a bare "Material" would come
            // out in the same pale grey for every element. An explicit swatch lets a match
            // that knows which element it is colour itself.
            colors.Add(go, swatch ?? SearchIndex.SwatchFor(kind));
            targets.Add(target);
        }

        /// <summary>
        /// Adds a candidate if its prefab tag was matched as <paramref name="kind"/>.
        /// </summary>
        private static void TryAddPrefab(TargetKind kind, KMonoBehaviour candidate,
                SearchMatches matches, List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            if (candidate == null || !IsRevealed(candidate))
                return;
            var kPrefabID = candidate.GetComponent<KPrefabID>();
            if (kPrefabID == null || !matches.HasPrefab(kind, kPrefabID.PrefabTag))
                return;
            Add(kind, candidate, targets, colors);
        }

        /// <summary>
        /// Adds a candidate of whatever kind its prefab tag was matched as, which is what a
        /// generic sweep needs since a loose item carries no useful component of its own.
        /// </summary>
        private static void TryAddMatchedPrefab(KMonoBehaviour candidate,
                SearchMatches matches, List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            if (candidate == null || !IsRevealed(candidate))
                return;
            var kPrefabID = candidate.GetComponent<KPrefabID>();
            if (kPrefabID == null)
                return;
            var kind = matches.PrefabKind(kPrefabID.PrefabTag);
            if (kind == null)
                return;
            Add(kind.Value, candidate, targets, colors);
        }

        /// <summary>
        /// Entities whose primary element matched, covering exactly the two things the mass
        /// label already reports for an element search: the loose items counted as debris and
        /// the buildings counted as building mass.
        /// <para>
        /// Both are matched by element rather than by prefab, because that is how the mass
        /// walks identify them, and the two had to agree: a built ore smelter and a lump of
        /// ore have no prefab entry of their own, so when the search resolved to the element
        /// alone the label would report their mass with nothing on screen lighting up.
        /// </para>
        /// <para>
        /// Highlighted only. The element count is a count of cells, and folding entities into
        /// it would report a different number again.
        /// </para>
        /// </summary>
        private static void CollectElementEntities(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var pickupables = Components.Pickupables.Items;
            for (int i = 0; i < pickupables.Count; i++) {
                var pe = pickupables[i].GetComponent<PrimaryElement>();
                if (pe == null || !matches.Elements.Contains(pe.ElementID))
                    continue;
                Add(TargetKind.Material, pickupables[i], targets, colors, count: false,
                    swatch: MaterialSearchOverlay.ElementSwatchFor(pe.ElementID));
            }
            var buildings = Components.BuildingCompletes.Items;
            for (int i = 0; i < buildings.Count; i++) {
                var pe = buildings[i].GetComponent<PrimaryElement>();
                if (pe == null || !matches.Elements.Contains(pe.ElementID))
                    continue;
                Add(TargetKind.Material, buildings[i], targets, colors, count: false,
                    swatch: MaterialSearchOverlay.ElementSwatchFor(pe.ElementID));
            }
        }

        /// <summary>
        /// True when the prefab is one the game itself calls an artifact. Used for the general
        /// artifact row, which has no tag of its own and so cannot be resolved through the
        /// indexed tags.
        /// </summary>
        private static bool IsArtifactPrefab(KPrefabID kPrefabID) {
            return kPrefabID != null &&
                (kPrefabID.HasTag(GameTags.Artifact) || kPrefabID.HasTag(GameTags.Keepsake));
        }

        private static void TryAddAnyArtifact(KMonoBehaviour candidate,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            if (candidate == null || !IsRevealed(candidate))
                return;
            if (!IsArtifactPrefab(candidate.GetComponent<KPrefabID>()))
                return;
            Add(TargetKind.Artifact, candidate, targets, colors);
        }

        /// <summary>
        /// Every artifact in the colony, for the general artifact row. Walks all three places
        /// an artifact can turn up rather than only the space artifact list, because a relic
        /// that has been brought home is no longer a space artifact: it can be lying in a
        /// corridor, displayed in a museum, or sitting in a crate.
        /// </summary>
        private static void CollectAnyArtifacts(List<KMonoBehaviour> targets,
                Dictionary<GameObject, Color> colors) {
            var artifacts = Components.SpaceArtifacts.Items;
            for (int i = 0; i < artifacts.Count; i++)
                TryAddAnyArtifact(artifacts[i], targets, colors);
            var pickupables = Components.Pickupables.Items;
            for (int i = 0; i < pickupables.Count; i++)
                TryAddAnyArtifact(pickupables[i], targets, colors);
            var buildings = Components.BuildingCompletes.Items;
            for (int i = 0; i < buildings.Count; i++)
                TryAddAnyArtifact(buildings[i], targets, colors);
            // Held artifacts are despawned while stored, so there is nothing to light up, but
            // the building holding them is the thing worth going to look at.
            for (int i = 0; i < buildings.Count; i++) {
                var building = buildings[i];
                if (building == null || !IsRevealed(building))
                    continue;
                if (!(building.GetComponent<IStorage>() is IStorage storage))
                    continue;
                var items = storage.GetItems();
                if (items == null)
                    continue;
                for (int j = 0; j < items.Count; j++) {
                    if (items[j] == null || !IsArtifactPrefab(items[j].GetComponent<KPrefabID>()))
                        continue;
                    Add(TargetKind.Artifact, building, targets, colors);
                    break;
                }
            }
        }

        /// <summary>
        /// Points at whatever is holding a matched item. Stored items are despawned while they
        /// are held, so there is no rendered object to light up, and they are in neither the
        /// assignable nor the pickupable list. The container is the thing the player can
        /// actually go and look at, so the building gets the highlight instead. Highlighted
        /// only: a rack holding a suit is not a second suit.
        /// </summary>
        private static void CollectStoredItems(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var buildings = Components.BuildingCompletes.Items;
            for (int i = 0; i < buildings.Count; i++) {
                var building = buildings[i];
                if (building == null || !IsRevealed(building))
                    continue;
                if (!(building.GetComponent<IStorage>() is IStorage storage))
                    continue;
                var items = storage.GetItems();
                if (items == null)
                    continue;
                for (int j = 0; j < items.Count; j++) {
                    var go = items[j];
                    if (go == null)
                        continue;
                    var kPrefabID = go.GetComponent<KPrefabID>();
                    if (kPrefabID == null)
                        continue;
                    var kind = matches.PrefabKind(kPrefabID.PrefabTag);
                    if (kind == null && !matches.Boosters.Contains(kPrefabID.PrefabTag))
                        continue;
                    Add(kind ?? TargetKind.Booster, building, targets, colors, count: false);
                    break;
                }
            }
        }

        /// <summary>
        /// Free floating items, which covers debris, food, seeds, industrial products and any
        /// equipment lying on the floor. Runs last so it only claims what nothing else did.
        /// </summary>
        private static void CollectPickupables(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var items = Components.Pickupables.Items;
            for (int i = 0; i < items.Count; i++)
                TryAddMatchedPrefab(items[i], matches, targets, colors);
        }

        private static void CollectBuildings(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var items = Components.BuildingCompletes.Items;
            for (int i = 0; i < items.Count; i++)
                TryAddMatchedPrefab(items[i], matches, targets, colors);
        }

        /// <summary>
        /// Plants and foods live on components that overlap arbitrarily (a crop is also
        /// harvestable and uprootable, a harvestable can be edible), so every relevant list
        /// is walked and the target set collapses the duplicates.
        /// </summary>
        private static void CollectPlantsAndFood(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var uprootables = Components.Uprootables.Items;
            for (int i = 0; i < uprootables.Count; i++)
                TryAddPrefab(TargetKind.Plant, uprootables[i], matches, targets, colors);
            var harvestables = Components.Harvestables.Items;
            for (int i = 0; i < harvestables.Count; i++)
                TryAddPrefab(TargetKind.Plant, harvestables[i], matches, targets, colors);
            var crops = Components.Crops.Items;
            for (int i = 0; i < crops.Count; i++)
                TryAddPrefab(TargetKind.Plant, crops[i], matches, targets, colors);
            var mutants = Components.MutantPlants.Items;
            for (int i = 0; i < mutants.Count; i++)
                TryAddPrefab(TargetKind.Plant, mutants[i], matches, targets, colors);
            var seeds = Components.PlantableSeeds.Items;
            for (int i = 0; i < seeds.Count; i++)
                TryAddPrefab(TargetKind.Plant, seeds[i], matches, targets, colors);
            var edibles = Components.Edibles.Items;
            for (int i = 0; i < edibles.Count; i++)
                TryAddPrefab(TargetKind.Food, edibles[i], matches, targets, colors);
        }

        private static void CollectArtifacts(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var artifacts = Components.SpaceArtifacts.Items;
            for (int i = 0; i < artifacts.Count; i++)
                TryAddPrefab(TargetKind.Artifact, artifacts[i], matches, targets, colors);
        }

        /// <summary>
        /// Equipment needs two passes. A worn suit has its KBatchedAnimController disabled
        /// by the game, so it cannot be lit up; the wearer is highlighted instead.
        /// </summary>
        private static void CollectEquipables(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var identities = Components.LiveMinionIdentities.Items;
            for (int i = 0; i < identities.Count; i++) {
                var identity = identities[i];
                if (identity == null || !IsRevealed(identity))
                    continue;
                var equipment = identity.GetEquipment();
                var slots = equipment?.Slots;
                if (slots == null)
                    continue;
                for (int s = 0; s < slots.Count; s++) {
                    if (!(slots[s]?.assignable is Equippable equippable))
                        continue;
                    var kPrefabID = equippable.GetComponent<KPrefabID>();
                    if (kPrefabID == null ||
                            !matches.HasPrefab(TargetKind.Equipment, kPrefabID.PrefabTag))
                        continue;
                    Add(TargetKind.Equipment, identity, targets, colors);
                    break;
                }
            }
            var assignables = Components.AssignableItems.Items;
            for (int i = 0; i < assignables.Count; i++) {
                if (!(assignables[i] is Equippable equippable) || equippable.isEquipped)
                    continue;
                TryAddPrefab(TargetKind.Equipment, equippable, matches, targets, colors);
            }
        }

        /// <summary>
        /// Critters have no component list of their own in this build, but every one of them
        /// carries a CreatureBrain and every Brain registers itself. MinionBrain is a sibling
        /// of CreatureBrain rather than a subclass, so the type check is required.
        /// </summary>
        private static void CollectCreatures(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var brains = Components.Brains.Items;
            for (int i = 0; i < brains.Count; i++) {
                if (!(brains[i] is CreatureBrain brain))
                    continue;
                if (brain.species == null || !matches.Species.Contains(brain.species))
                    continue;
                if (!IsRevealed(brain))
                    continue;
                Add(TargetKind.Critter, brain, targets, colors);
            }
        }

        /// <summary>
        /// Duplicants by model, plus the bionic duplicants wearing a searched booster. The two
        /// model searches are exact complements, so a plain duplicant search never lights up a
        /// bionic one and vice versa. All three resolve from the same identity pass.
        /// </summary>
        private static void CollectDuplicants(SearchMatches matches,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            if (matches.Boosters.Count > 0) {
                // Gathered first: an installed booster is still a live object sitting in the
                // bionic's storage, so it would otherwise be counted once as its own
                // component and again as the duplicant wearing it.
                var installed = new HashSet<BionicUpgradeComponent>();
                CollectInstalledBoosters(installed);
                CollectLooseBoosters(matches, installed, targets, colors);
            }
            if (!matches.MatchBionics && !matches.MatchDupes &&
                    matches.Boosters.Count == 0)
                return;
            var identities = Components.LiveMinionIdentities.Items;
            for (int i = 0; i < identities.Count; i++) {
                var identity = identities[i];
                if (identity == null || !IsRevealed(identity))
                    continue;
                var isBionic = identity.model == GameTags.Minions.Models.Bionic;
                if (isBionic ? matches.MatchBionics : matches.MatchDupes)
                    Add(isBionic ? TargetKind.Bionic : TargetKind.Duplicant, identity, targets,
                        colors);
                if (matches.Boosters.Count > 0 && isBionic &&
                        WearsMatchingBooster(identity, matches.Boosters))
                    Add(TargetKind.Booster, identity, targets, colors);
            }
        }

        private static bool WearsMatchingBooster(MinionIdentity identity,
                HashSet<Tag> boosters) {
            var monitor = identity.gameObject.GetSMI<BionicUpgradesMonitor.Instance>();
            var slots = monitor?.upgradeComponentSlots;
            if (slots == null)
                return false;
            for (int i = 0; i < slots.Length; i++) {
                var slot = slots[i];
                if (slot == null || !slot.HasUpgradeInstalled)
                    continue;
                if (boosters.Contains(slot.InstalledUpgradeID))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Every booster that is currently installed in a bionic, as the component instances
        /// themselves so they can be told apart from a loose booster of the same kind.
        /// </summary>
        private static void CollectInstalledBoosters(
                HashSet<BionicUpgradeComponent> into) {
            var identities = Components.LiveMinionIdentities.Items;
            for (int i = 0; i < identities.Count; i++) {
                if (identities[i] == null)
                    continue;
                var monitor = identities[i].gameObject.GetSMI<BionicUpgradesMonitor.Instance>();
                var slots = monitor?.upgradeComponentSlots;
                if (slots == null)
                    continue;
                for (int s = 0; s < slots.Length; s++) {
                    var installed = slots[s]?.installedUpgradeComponent;
                    if (installed != null)
                        into.Add(installed);
                }
            }
        }

        /// <summary>
        /// Boosters that are assigned but not installed sit in the bionic storage as
        /// assignables, and those are still physical objects worth pointing at. Installed ones
        /// are skipped: the duplicant wearing them already represents them.
        /// </summary>
        private static void CollectLooseBoosters(SearchMatches matches,
                HashSet<BionicUpgradeComponent> installed,
                List<KMonoBehaviour> targets, Dictionary<GameObject, Color> colors) {
            var assignables = Components.AssignableItems.Items;
            for (int i = 0; i < assignables.Count; i++) {
                if (!(assignables[i] is BionicUpgradeComponent booster))
                    continue;
                if (installed.Contains(booster))
                    continue;
                if (!IsRevealed(booster))
                    continue;
                var kPrefabID = booster.GetComponent<KPrefabID>();
                if (kPrefabID == null || !matches.Boosters.Contains(kPrefabID.PrefabTag))
                    continue;
                Add(TargetKind.Booster, booster, targets, colors);
            }
        }
    }
}
