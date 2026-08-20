// -----------------------------------------------------------------------------
// TagDefinition.cs
//
// Represents one unique, normalized tag in the BrickView tag domain.
//
// Tags are case-insensitive and are always stored as lowercase.
//
// BrickView assigns each tag a stable background and border color. The color
// is part of the tag definition so the same tag has the same appearance on
// every model and across application restarts.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagDefinition {
    public string Name { get; }

    public string BackgroundColor { get; private set; }

    public string BorderColor { get; private set; }

    public TagDefinition(
        string name) {
        string normalizedName =
            NormalizeName(name);

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

    internal void SetColors(
        TagColorDefinition color) {
        ArgumentNullException.ThrowIfNull(
            color);

        BackgroundColor =
            color.BackgroundColor;

        BorderColor =
            color.BorderColor;
    }

    private static string NormalizeName(
        string name) {
        if (name is null) {
            throw new ArgumentNullException(
                nameof(name));
        }

        return name.Trim().ToLowerInvariant();
    }

    public override string ToString() {
        return Name;
    }

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

    public override int GetHashCode() {
        return StringComparer.Ordinal.GetHashCode(
            Name);
    }
}