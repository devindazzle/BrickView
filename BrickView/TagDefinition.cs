// -----------------------------------------------------------------------------
// TagDefinition.cs
//
// Represents one unique, normalized tag in the BrickView tag domain.
//
// Responsibilities:
// - Stores the normalized name of a tag.
// - Defines tag equality through the normalized name.
// - Stores the stable background and border colors assigned to the tag.
// - Provides controlled internal color assignment through SetColors().
//
// Tags are case-insensitive and are always stored as lowercase. Leading and
// trailing whitespace is removed during normalization.
//
// BrickView assigns each tag a stable background and border color. The color is
// part of the tag definition so the same tag has the same appearance on every
// model and across application restarts.
//
// TagDefinition represents the tag itself. Tag collection management and
// persistence are handled by the surrounding tag infrastructure.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents one unique, normalized tag in the BrickView tag domain.
/// </summary>
public sealed class TagDefinition {
    /// <summary>
    /// Gets the normalized name of the tag.
    /// </summary>
    /// <remarks>
    /// The name is trimmed and converted to lowercase when the tag is created.
    /// </remarks>
    public string Name {
        get;
    }

    /// <summary>
    /// Gets the hexadecimal background color assigned to the tag.
    /// </summary>
    /// <remarks>
    /// The color is assigned by BrickView's tag-color infrastructure and is
    /// intentionally not publicly settable.
    /// </remarks>
    public string BackgroundColor {
        get;
        private set;
    }

    /// <summary>
    /// Gets the hexadecimal border color assigned to the tag.
    /// </summary>
    /// <remarks>
    /// The color is assigned by BrickView's tag-color infrastructure and is
    /// intentionally not publicly settable.
    /// </remarks>
    public string BorderColor {
        get;
        private set;
    }

    /// <summary>
    /// Initializes a new tag definition and normalizes its name.
    /// </summary>
    /// <param name="name">
    /// The name of the tag.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the normalized tag name is empty.
    /// </exception>
    public TagDefinition(
        string name) {
        string normalizedName =
            NormalizeName(
                name);

        if (string.IsNullOrEmpty(
                normalizedName)) {
            throw new ArgumentException(
                "A tag cannot be empty.",
                nameof(name));
        }

        Name =
            normalizedName;

        BackgroundColor =
            string.Empty;

        BorderColor =
            string.Empty;
    }

    /// <summary>
    /// Assigns the background and border colors for this tag.
    /// </summary>
    /// <param name="color">
    /// The color definition to assign.
    /// </param>
    /// <remarks>
    /// This method is internal because tag colors are controlled by BrickView's
    /// tag-color infrastructure rather than by arbitrary callers.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="color"/> is null.
    /// </exception>
    internal void SetColors(
        TagColorDefinition color) {
        ArgumentNullException.ThrowIfNull(
            color);

        BackgroundColor =
            color.BackgroundColor;

        BorderColor =
            color.BorderColor;
    }

    /// <summary>
    /// Normalizes a tag name by removing surrounding whitespace and converting
    /// it to lowercase using invariant casing rules.
    /// </summary>
    /// <param name="name">
    /// The tag name to normalize.
    /// </param>
    /// <returns>
    /// The normalized tag name.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    private static string NormalizeName(
        string name) {
        if (name is null) {
            throw new ArgumentNullException(
                nameof(name));
        }

        // Normalization makes tag identity case-insensitive and prevents
        // accidental differences caused by surrounding whitespace.
        return name
            .Trim()
            .ToLowerInvariant();
    }

    /// <summary>
    /// Returns the normalized tag name.
    /// </summary>
    /// <returns>
    /// The normalized tag name.
    /// </returns>
    public override string ToString() {
        return Name;
    }

    /// <summary>
    /// Determines whether another object represents the same tag.
    /// Equality is based solely on the normalized tag name.
    /// </summary>
    /// <param name="obj">
    /// The object to compare with this tag definition.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="obj"/> is a
    /// <see cref="TagDefinition"/> with the same normalized name;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(
        object? obj) {
        if (obj is not TagDefinition other) {
            return false;
        }

        return string.Equals(
            Name,
            other.Name,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a hash code based on the normalized tag name.
    /// </summary>
    /// <returns>
    /// The hash code for this tag definition.
    /// </returns>
    public override int GetHashCode() {
        return StringComparer.Ordinal.GetHashCode(
            Name);
    }
}