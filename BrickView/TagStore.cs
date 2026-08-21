// -----------------------------------------------------------------------------
// TagStore.cs
//
// Defines the persistent data models used by BrickView's tag system.
//
// This file contains the data structures serialized to and restored from the
// persistent tag store:
//
// - TagStore
//     Contains the complete persisted tag catalog and model metadata.
//
// - TagStoreDefinition
//     Stores the persisted name and assigned colors of one reusable tag.
//
// - ModelTagStoreEntry
//     Stores the stable model identity, assigned tags and Favorite state for
//     one model.
//
// Responsibilities:
// - Represent persistent tag and model metadata.
// - Provide default values required for JSON deserialization.
// - Normalize persisted data before it enters the runtime tag system.
// - Remove invalid and duplicate data.
// - Ensure model tag assignments follow BrickView's tag-count limit.
//
// These classes are persistence models rather than runtime domain objects.
// Runtime tag behavior is handled by TagService, TagCatalog,
// ModelTagCollection and TagDefinition.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the complete persistent data store for BrickView's tag system.
/// </summary>
/// <remarks>
/// The store contains the reusable tag catalog and the tag assignments and
/// Favorite state of individual models.
///
/// <see cref="Normalize"/> should be called after loading persisted data and
/// before the data is used by the runtime tag system.
/// </remarks>
public sealed class TagStore {
    /// <summary>
    /// Gets or sets the names of all tags known to the persisted tag catalog.
    /// </summary>
    public List<string> Tags {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the persisted definitions, including colors, for known tags.
    /// </summary>
    public List<TagStoreDefinition> TagDefinitions {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the persisted metadata for individual models.
    /// </summary>
    public List<ModelTagStoreEntry> Models {
        get;
        set;
    }

    /// <summary>
    /// Initializes an empty tag store.
    /// </summary>
    public TagStore() {
        Tags =
            new List<string>();

        TagDefinitions =
            new List<TagStoreDefinition>();

        Models =
            new List<ModelTagStoreEntry>();
    }

    /// <summary>
    /// Normalizes all persisted tag and model data according to BrickView's
    /// tag-domain rules.
    /// </summary>
    /// <remarks>
    /// Normalization:
    /// - normalizes tag names;
    /// - removes invalid tag definitions;
    /// - removes duplicate tag definitions;
    /// - combines tags from the legacy and definition collections;
    /// - creates missing tag definitions for legacy tags;
    /// - removes invalid model entries;
    /// - removes duplicate model identities;
    /// - removes model tags that are not present in the catalog;
    /// - limits each model to the maximum allowed number of tags.
    /// </remarks>
    public void Normalize() {
        Dictionary<string, string> normalizedTags =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        Dictionary<string, TagStoreDefinition> normalizedDefinitions =
            new Dictionary<string, TagStoreDefinition>(
                StringComparer.Ordinal);

        foreach (TagStoreDefinition definition
                 in TagDefinitions) {

            definition.Normalize();

            if (string.IsNullOrWhiteSpace(
                    definition.Name)) {
                continue;
            }

            if (!normalizedDefinitions.ContainsKey(
                    definition.Name)) {

                // Keep the first definition for a normalized tag name so
                // duplicate persisted definitions cannot overwrite each other.
                normalizedDefinitions.Add(
                    definition.Name,
                    definition);
            }

            normalizedTags.TryAdd(
                definition.Name,
                definition.Name);
        }

        foreach (string tag in Tags) {
            if (string.IsNullOrWhiteSpace(
                    tag)) {
                continue;
            }

            string normalizedTag =
                tag.Trim().ToLowerInvariant();

            normalizedTags.TryAdd(
                normalizedTag,
                normalizedTag);
        }

        foreach (ModelTagStoreEntry model
                 in Models) {

            model.Normalize();

            foreach (string tag
                     in model.Tags) {

                // A model may reference a tag that was not explicitly present
                // in the legacy catalog. Preserve that tag by adding it to the
                // normalized catalog.
                normalizedTags.TryAdd(
                    tag,
                    tag);
            }
        }

        Tags =
            normalizedTags.Keys
                .OrderBy(
                    tag =>
                        tag,
                    StringComparer.Ordinal)
                .ToList();

        // Every normalized tag must have a persisted definition. Legacy tags
        // without color information receive an empty definition and can later
        // be assigned colors by the runtime tag infrastructure.
        TagDefinitions =
            Tags
                .Select(
                    tagName =>
                        normalizedDefinitions.TryGetValue(
                            tagName,
                            out TagStoreDefinition? definition)
                            ? definition
                            : new TagStoreDefinition(
                                tagName,
                                string.Empty,
                                string.Empty))
                .ToList();

        // Model identity is the stable key. If corrupted or duplicated
        // persistence data contains the same identity more than once, keep
        // the first entry.
        Models =
            Models
                .Where(
                    model =>
                        !string.IsNullOrWhiteSpace(
                            model.ModelId))
                .GroupBy(
                    model =>
                        model.ModelId,
                    StringComparer.Ordinal)
                .Select(
                    group =>
                        group.First())
                .ToList();

        HashSet<string> catalogTags =
            new HashSet<string>(
                Tags,
                StringComparer.Ordinal);

        foreach (ModelTagStoreEntry model
                 in Models) {

            // A persisted model can only retain tags that exist in the
            // normalized catalog and must respect the three-tag limit.
            model.Tags =
                model.Tags
                    .Where(
                        tag =>
                            catalogTags.Contains(
                                tag))
                    .Take(
                        ModelTagCollection.MaximumTagCount)
                    .ToList();
        }
    }
}

/// <summary>
/// Represents the persisted definition of one reusable BrickView tag.
/// </summary>
/// <remarks>
/// This class intentionally stores strings rather than a runtime
/// <see cref="TagDefinition"/> so it can be serialized directly.
/// </remarks>
public sealed class TagStoreDefinition {
    /// <summary>
    /// Gets or sets the normalized tag name.
    /// </summary>
    public string Name {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the persisted hexadecimal background color.
    /// </summary>
    public string BackgroundColor {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the persisted hexadecimal border color.
    /// </summary>
    public string BorderColor {
        get;
        set;
    }

    /// <summary>
    /// Initializes an empty tag definition for JSON deserialization.
    /// </summary>
    public TagStoreDefinition() {
        Name =
            string.Empty;

        BackgroundColor =
            string.Empty;

        BorderColor =
            string.Empty;
    }

    /// <summary>
    /// Initializes a persisted tag definition with the supplied values and
    /// immediately normalizes them.
    /// </summary>
    /// <param name="name">
    /// The tag name.
    /// </param>
    /// <param name="backgroundColor">
    /// The persisted background color.
    /// </param>
    /// <param name="borderColor">
    /// The persisted border color.
    /// </param>
    public TagStoreDefinition(
        string name,
        string backgroundColor,
        string borderColor) {
        Name =
            name;

        BackgroundColor =
            backgroundColor;

        BorderColor =
            borderColor;

        Normalize();
    }

    /// <summary>
    /// Normalizes the persisted tag definition.
    /// </summary>
    /// <remarks>
    /// Tag names are trimmed and converted to lowercase. Color values are
    /// trimmed but otherwise preserved so valid persisted colors are not
    /// altered during loading.
    ///
    /// When the name is invalid, all fields are cleared because the definition
    /// cannot represent a usable persisted tag.
    /// </remarks>
    public void Normalize() {
        if (string.IsNullOrWhiteSpace(
                Name)) {

            Name =
                string.Empty;

            BackgroundColor =
                string.Empty;

            BorderColor =
                string.Empty;

            return;
        }

        Name =
            Name.Trim().ToLowerInvariant();

        BackgroundColor =
            BackgroundColor?.Trim() ??
            string.Empty;

        BorderColor =
            BorderColor?.Trim() ??
            string.Empty;
    }
}

/// <summary>
/// Represents the persisted metadata associated with one BrickView model.
/// </summary>
/// <remarks>
/// The model is identified by its stable <see cref="ModelId"/>. Its assigned
/// tags and Favorite state are persisted together so both types of model
/// metadata follow the same identity lifecycle.
/// </remarks>
public sealed class ModelTagStoreEntry {
    /// <summary>
    /// Gets or sets the stable identity of the model.
    /// </summary>
    public string ModelId {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the normalized tag names assigned to the model.
    /// </summary>
    public List<string> Tags {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the model is marked as a Favorite.
    /// </summary>
    public bool IsFavorite {
        get;
        set;
    }

    /// <summary>
    /// Initializes an empty model metadata entry for JSON deserialization.
    /// </summary>
    public ModelTagStoreEntry() {
        ModelId =
            string.Empty;

        Tags =
            new List<string>();

        IsFavorite =
            false;
    }

    /// <summary>
    /// Initializes a model metadata entry with a stable identity and tag list.
    /// </summary>
    /// <param name="modelId">
    /// The stable model identity.
    /// </param>
    /// <param name="tags">
    /// The tags assigned to the model.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="modelId"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tags"/> is null.
    /// </exception>
    public ModelTagStoreEntry(
        string modelId,
        IEnumerable<string> tags) {
        if (string.IsNullOrWhiteSpace(
                modelId)) {
            throw new ArgumentException(
                "A model identity cannot be empty.",
                nameof(modelId));
        }

        ArgumentNullException.ThrowIfNull(
            tags);

        ModelId =
            modelId;

        Tags =
            tags.ToList();

        IsFavorite =
            false;

        Normalize();
    }

    /// <summary>
    /// Normalizes the persisted model metadata.
    /// </summary>
    /// <remarks>
    /// The model identity is trimmed. Tags are trimmed, converted to lowercase,
    /// deduplicated and limited to the maximum number allowed per model.
    ///
    /// An invalid model identity causes the tag list to be cleared because the
    /// entry cannot represent valid model metadata.
    /// </remarks>
    public void Normalize() {
        if (string.IsNullOrWhiteSpace(
                ModelId)) {

            Tags =
                new List<string>();

            return;
        }

        ModelId =
            ModelId.Trim();

        HashSet<string> normalizedTags =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (string tag
                 in Tags) {

            if (string.IsNullOrWhiteSpace(
                    tag)) {
                continue;
            }

            string normalizedTag =
                tag.Trim().ToLowerInvariant();

            normalizedTags.Add(
                normalizedTag);
        }

        Tags =
            normalizedTags
                .Take(
                    ModelTagCollection.MaximumTagCount)
                .ToList();
    }
}