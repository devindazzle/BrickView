// -----------------------------------------------------------------------------
// TagCatalog.cs
//
// Maintains the unique, reusable tag definitions used by BrickView.
//
// Responsibilities:
// - Maintains one TagDefinition instance for each unique tag name.
// - Provides case-insensitive tag lookup through TagDefinition normalization.
// - Creates missing tag definitions on demand.
// - Assigns a stable color to tags that do not already have persisted colors.
// - Preserves supplied persisted colors when they are valid and available.
// - Provides lookup, removal and clearing of tag definitions.
//
// TagCatalog owns the in-memory collection of reusable tag definitions.
// It does not persist tags itself; persistence is handled by the tag
// persistence/service layer.
//
// Tag colors are selected through TagColorPalette. A background color already
// used by another tag is not reused when a persisted color is restored.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Maintains the unique, reusable tag definitions used by BrickView.
/// </summary>
public sealed class TagCatalog {
    private readonly Dictionary<string, TagDefinition> tags;

    /// <summary>
    /// Gets all tag definitions currently contained in the catalog.
    /// </summary>
    public IReadOnlyCollection<TagDefinition> Tags {
        get {
            return tags.Values;
        }
    }

    /// <summary>
    /// Gets the number of tag definitions currently contained in the catalog.
    /// </summary>
    public int Count {
        get {
            return tags.Count;
        }
    }

    /// <summary>
    /// Initializes an empty tag catalog.
    /// </summary>
    public TagCatalog() {
        // TagDefinition normalizes names before they are used as dictionary
        // keys, so ordinal comparison is sufficient here.
        tags =
            new Dictionary<string, TagDefinition>(
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets an existing tag definition with the specified name or creates a
    /// new tag definition when no matching tag exists.
    /// </summary>
    /// <param name="name">
    /// The name of the tag to find or create.
    /// </param>
    /// <returns>
    /// The existing or newly created tag definition.
    /// </returns>
    public TagDefinition GetOrCreate(
        string name) {
        TagDefinition candidate =
            new TagDefinition(
                name);

        if (tags.TryGetValue(
                candidate.Name,
                out TagDefinition? existingTag)) {

            // An existing tag may originate from older persisted data without
            // a color. Ensure it has a usable color before returning it.
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

    /// <summary>
    /// Gets an existing tag definition with the specified name or creates a
    /// new tag definition using the supplied persisted colors when possible.
    /// </summary>
    /// <param name="name">
    /// The name of the tag to find or create.
    /// </param>
    /// <param name="backgroundColor">
    /// The persisted background color, when one is available.
    /// </param>
    /// <param name="borderColor">
    /// The persisted border color, when one is available.
    /// </param>
    /// <returns>
    /// The existing or newly created tag definition.
    /// </returns>
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

            // Never replace an existing tag definition. Only ensure that it
            // has a valid color before returning the shared instance.
            EnsureColor(
                existingTag);

            return existingTag;
        }

        // A persisted color pair is restored only when both colors are present
        // and the background color is not already assigned to another tag.
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

    /// <summary>
    /// Attempts to find a tag definition with the specified name.
    /// </summary>
    /// <param name="name">
    /// The name of the tag to find.
    /// </param>
    /// <param name="tag">
    /// When the method returns <see langword="true"/>, contains the matching
    /// tag definition; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a matching tag exists; otherwise,
    /// <see langword="false"/>.
    /// </returns>
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

        // Tags loaded from persistence may not have a color. Repair that
        // state before exposing the tag to callers.
        EnsureColor(
            tag);

        return true;
    }

    /// <summary>
    /// Removes the specified tag definition from the catalog.
    /// </summary>
    /// <param name="tag">
    /// The tag definition to remove.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the tag existed and was removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Remove(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Remove(
            tag.Name);
    }

    /// <summary>
    /// Removes all tag definitions from the catalog.
    /// </summary>
    public void Clear() {
        tags.Clear();
    }

    /// <summary>
    /// Ensures that the specified tag has both required colors.
    /// </summary>
    /// <param name="tag">
    /// The tag whose color assignment should be checked.
    /// </param>
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

    /// <summary>
    /// Assigns the next suitable color from the BrickView tag-color palette
    /// to the specified tag.
    /// </summary>
    /// <param name="tag">
    /// The tag that should receive a color.
    /// </param>
    private void AssignNewColor(
        TagDefinition tag) {
        TagColorDefinition color =
            TagColorPalette.GetColor(
                tag.Name,
                tags.Values);

        tag.SetColors(
            color);
    }

    /// <summary>
    /// Determines whether the specified background color is not already used
    /// by another tag in the catalog.
    /// </summary>
    /// <param name="backgroundColor">
    /// The background color to check.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when no existing tag uses the specified background
    /// color; otherwise, <see langword="false"/>.
    /// </returns>
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