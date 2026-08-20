// -----------------------------------------------------------------------------
// TagStore.cs
//
// Persistent data model for BrickView's tag system.
//
// The store contains the reusable tag catalog and the tag assignments for
// individual models.
//
// Legacy tag names are retained for backward compatibility. TagDefinitions
// stores the newer color information assigned by BrickView.
//
// The Normalize method ensures that loaded persistence data always follows
// BrickView's tag rules.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagStore {
    public List<string> Tags { get; set; }

    public List<TagStoreDefinition> TagDefinitions { get; set; }

    public List<ModelTagStoreEntry> Models { get; set; }

    public TagStore() {
        Tags =
            new List<string>();

        TagDefinitions =
            new List<TagStoreDefinition>();

        Models =
            new List<ModelTagStoreEntry>();
    }

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

public sealed class TagStoreDefinition {
    public string Name { get; set; }

    public string BackgroundColor { get; set; }

    public string BorderColor { get; set; }

    public TagStoreDefinition() {
        Name =
            string.Empty;

        BackgroundColor =
            string.Empty;

        BorderColor =
            string.Empty;
    }

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

public sealed class ModelTagStoreEntry {
    public string ModelId { get; set; }

    public List<string> Tags { get; set; }

    public ModelTagStoreEntry() {
        ModelId =
            string.Empty;

        Tags =
            new List<string>();
    }

    public ModelTagStoreEntry(
        string modelId,
        IEnumerable<string> tags) {
        if (string.IsNullOrWhiteSpace(
                modelId)) {
            throw new ArgumentException(
                "A model identity cannot be empty.",
                nameof(modelId));
        }

        ModelId =
            modelId;

        Tags =
            tags.ToList();

        Normalize();
    }

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