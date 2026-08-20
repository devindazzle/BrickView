// -----------------------------------------------------------------------------
// ModelTagCollection.cs
//
// Represents the tags assigned to one BrickView model.
//
// A model may have a maximum of three tags. Tags are unique and case-insensitive
// because TagDefinition normalizes every tag to lowercase.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class ModelTagCollection {
    public const int MaximumTagCount = 3;

    private readonly List<TagDefinition> tags;

    public IReadOnlyList<TagDefinition> Tags {
        get {
            return tags;
        }
    }

    public int Count {
        get {
            return tags.Count;
        }
    }

    public ModelTagCollection() {
        tags =
            new List<TagDefinition>();
    }

    public bool Add(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        if (tags.Contains(
                tag)) {
            return false;
        }

        if (tags.Count >= MaximumTagCount) {
            return false;
        }

        tags.Add(
            tag);

        return true;
    }

    public bool Remove(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Remove(
            tag);
    }

    public bool Contains(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Contains(
            tag);
    }

    public void Clear() {
        tags.Clear();
    }
}