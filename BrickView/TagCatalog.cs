// -----------------------------------------------------------------------------
// TagCatalog.cs
//
// Maintains the unique, reusable tag definitions used by BrickView.
//
// Tags are case-insensitive and are normalized to lowercase by TagDefinition.
// A single tag definition can therefore be shared by any number of models.
//
// BrickView automatically assigns a stable color to each tag. Existing
// persisted colors are preserved when supplied; tags without colors receive a
// new color from the BrickView palette.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagCatalog {
    private readonly Dictionary<string, TagDefinition> tags;

    public IReadOnlyCollection<TagDefinition> Tags {
        get {
            return tags.Values;
        }
    }

    public int Count {
        get {
            return tags.Count;
        }
    }

    public TagCatalog() {
        tags =
            new Dictionary<string, TagDefinition>(
                StringComparer.Ordinal);
    }

    public TagDefinition GetOrCreate(
        string name) {
        TagDefinition candidate =
            new TagDefinition(
                name);

        if (tags.TryGetValue(
                candidate.Name,
                out TagDefinition? existingTag)) {

            EnsureColor(
                existingTag);

            return existingTag;
        }

        AssignNewColor(
            candidate);

        tags.Add(
            candidate.Name,
            candidate);

        return candidate;
    }

    public TagDefinition GetOrCreate(
        string name,
        string? backgroundColor,
        string? borderColor) {
        TagDefinition candidate =
            new TagDefinition(
                name);

        if (tags.TryGetValue(
                candidate.Name,
                out TagDefinition? existingTag)) {

            EnsureColor(
                existingTag);

            return existingTag;
        }

        if (!string.IsNullOrWhiteSpace(
                backgroundColor) &&
            !string.IsNullOrWhiteSpace(
                borderColor) &&
            IsBackgroundColorAvailable(
                backgroundColor)) {

            candidate.SetColors(
                new TagColorDefinition(
                    backgroundColor,
                    borderColor));
        }
        else {
            AssignNewColor(
                candidate);
        }

        tags.Add(
            candidate.Name,
            candidate);

        return candidate;
    }

    public bool TryGet(
        string name,
        out TagDefinition? tag) {
        TagDefinition candidate =
            new TagDefinition(
                name);

        if (!tags.TryGetValue(
                candidate.Name,
                out tag)) {
            return false;
        }

        EnsureColor(
            tag);

        return true;
    }

    public bool Remove(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Remove(
            tag.Name);
    }

    public void Clear() {
        tags.Clear();
    }

    private void EnsureColor(
        TagDefinition tag) {
        if (!string.IsNullOrWhiteSpace(
                tag.BackgroundColor) &&
            !string.IsNullOrWhiteSpace(
                tag.BorderColor)) {
            return;
        }

        AssignNewColor(
            tag);
    }

    private void AssignNewColor(
        TagDefinition tag) {
        TagColorDefinition color =
            TagColorPalette.GetColor(
                tag.Name,
                tags.Values);

        tag.SetColors(
            color);
    }

    private bool IsBackgroundColorAvailable(
        string backgroundColor) {
        return !tags.Values.Any(
            tag =>
                string.Equals(
                    tag.BackgroundColor,
                    backgroundColor,
                    StringComparison.OrdinalIgnoreCase));
    }
}