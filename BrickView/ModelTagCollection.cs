// -----------------------------------------------------------------------------
// ModelTagCollection.cs
//
// Represents the collection of tags assigned to one BrickView model.
//
// Responsibilities:
// - Stores the tags assigned to a single model.
// - Prevents duplicate tags.
// - Enforces the maximum number of tags allowed per model.
// - Provides read-only access to the assigned tags.
//
// A model may have a maximum of three tags. Tags are unique and case-insensitive
// because TagDefinition normalizes every tag to lowercase.
//
// The collection owns tag membership for one model but does not persist tags.
// Persistence is handled by the tag service layer.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the collection of tags assigned to one BrickView model.
/// </summary>
public sealed class ModelTagCollection {
    /// <summary>
    /// Gets the maximum number of tags that can be assigned to one model.
    /// </summary>
    public const int MaximumTagCount = 3;

    private readonly List<TagDefinition> tags;

    /// <summary>
    /// Gets the tags currently assigned to the model.
    /// </summary>
    /// <remarks>
    /// The returned collection is read-only, so callers cannot modify the
    /// collection without going through this class.
    /// </remarks>
    public IReadOnlyList<TagDefinition> Tags {
        get {
            return tags;
        }
    }

    /// <summary>
    /// Gets the number of tags currently assigned to the model.
    /// </summary>
    public int Count {
        get {
            return tags.Count;
        }
    }

    /// <summary>
    /// Initializes an empty tag collection.
    /// </summary>
    public ModelTagCollection() {
        tags =
            new List<TagDefinition>();
    }

    /// <summary>
    /// Adds a tag to the collection when the tag is not already present and
    /// the maximum tag count has not been reached.
    /// </summary>
    /// <param name="tag">The tag to add.</param>
    /// <returns>
    /// <see langword="true"/> when the tag was added; otherwise,
    /// <see langword="false"/> when it was already present or the collection
    /// already contains <see cref="MaximumTagCount"/> tags.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tag"/> is <see langword="null"/>.
    /// </exception>
    public bool Add(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        if (tags.Contains(
                tag)) {
            return false;
        }

        // A model is limited to three tags. Keep this rule in the collection
        // so all callers receive the same behavior.
        if (tags.Count >= MaximumTagCount) {
            return false;
        }

        tags.Add(
            tag);

        return true;
    }

    /// <summary>
    /// Removes a tag from the collection.
    /// </summary>
    /// <param name="tag">The tag to remove.</param>
    /// <returns>
    /// <see langword="true"/> when the tag was present and removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tag"/> is <see langword="null"/>.
    /// </exception>
    public bool Remove(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Remove(
            tag);
    }

    /// <summary>
    /// Determines whether the specified tag is assigned to the model.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>
    /// <see langword="true"/> when the tag is present; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tag"/> is <see langword="null"/>.
    /// </exception>
    public bool Contains(
        TagDefinition tag) {
        ArgumentNullException.ThrowIfNull(
            tag);

        return tags.Contains(
            tag);
    }

    /// <summary>
    /// Removes all tags currently assigned to the model.
    /// </summary>
    public void Clear() {
        tags.Clear();
    }
}