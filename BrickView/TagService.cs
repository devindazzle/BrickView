// -----------------------------------------------------------------------------
// TagService.cs
//
// Provides the runtime business logic for BrickView's tag system and
// model-level metadata.
//
// Responsibilities:
// - Manages the reusable tag catalog.
// - Manages model-to-tag relationships.
// - Manages model Favorite state.
// - Enforces the maximum of three tags per model.
// - Normalizes tag names through TagDefinition.
// - Coordinates persistence of model metadata.
// - Removes a tag globally from the tag catalog and every model that uses it.
// - Handles migration of persisted model identity when a model is renamed or
//   otherwise receives a new stable identity.
//
// TagService keeps tag and Favorite data in memory so normal lookups do not
// require disk I/O. Persistent storage is handled by TagPersistenceService.
//
// Favorites are model metadata and are deliberately kept separate from tags.
// Both are persisted using the same stable ModelIdentity-based model entry.
//
// Global tag deletion is handled here rather than in the UI so the operation
// remains consistent regardless of where it is initiated.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Provides the runtime business logic for BrickView's tag system and
/// model-level metadata.
/// </summary>
public sealed class TagService {
    private readonly TagPersistenceService persistenceService;

    private readonly TagCatalog tagCatalog;

    private readonly Dictionary<string, ModelTagCollection> modelTags;

    private readonly HashSet<string> favoriteModelIdentities;

    /// <summary>
    /// Initializes the tag service and loads the persisted tag and model
    /// metadata.
    /// </summary>
    /// <param name="persistenceService">
    /// The service responsible for loading and saving the persistent tag store.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="persistenceService"/> is null.
    /// </exception>
    public TagService(
        TagPersistenceService persistenceService) {
        ArgumentNullException.ThrowIfNull(
            persistenceService);

        this.persistenceService =
            persistenceService;

        tagCatalog =
            new TagCatalog();

        modelTags =
            new Dictionary<string, ModelTagCollection>(
                StringComparer.Ordinal);

        favoriteModelIdentities =
            new HashSet<string>(
                StringComparer.Ordinal);

        Load();
    }

    /// <summary>
    /// Gets all tag definitions currently known to the application.
    /// </summary>
    /// <returns>
    /// A read-only collection containing all known tag definitions.
    /// </returns>
    public IReadOnlyCollection<TagDefinition> GetAllTags() {
        return tagCatalog.Tags;
    }

    /// <summary>
    /// Attempts to find a tag definition by name.
    /// </summary>
    /// <param name="tagName">
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
    public bool TryGetTag(
        string tagName,
        out TagDefinition? tag) {
        return tagCatalog.TryGet(
            tagName,
            out tag);
    }

    /// <summary>
    /// Gets all tags assigned to the specified model.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    /// <returns>
    /// A read-only list of the model's assigned tags, or an empty list when
    /// no tags are assigned.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelIdentity"/> is null.
    /// </exception>
    public IReadOnlyList<TagDefinition> GetTags(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        if (!modelTags.TryGetValue(
                modelIdentity.Value,
                out ModelTagCollection? collection)) {
            return Array.Empty<TagDefinition>();
        }

        return collection.Tags;
    }

    /// <summary>
    /// Determines whether the specified model is marked as a Favorite.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the model is marked as a Favorite;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelIdentity"/> is null.
    /// </exception>
    public bool IsFavorite(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        return favoriteModelIdentities.Contains(
            modelIdentity.Value);
    }

    /// <summary>
    /// Sets the Favorite state of the specified model.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    /// <param name="isFavorite">
    /// <see langword="true"/> to mark the model as a Favorite;
    /// <see langword="false"/> to remove the Favorite state.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the Favorite state changed; otherwise,
    /// <see langword="false"/> when the requested state was already active.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelIdentity"/> is null.
    /// </exception>
    public bool SetFavorite(
        ModelIdentity modelIdentity,
        bool isFavorite) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        bool changed;

        if (isFavorite) {
            changed =
                favoriteModelIdentities.Add(
                    modelIdentity.Value);
        }
        else {
            changed =
                favoriteModelIdentities.Remove(
                    modelIdentity.Value);
        }

        if (!changed) {
            return false;
        }

        // Persist only after an actual state change so redundant UI operations
        // do not cause unnecessary disk writes.
        Save();

        return true;
    }

    /// <summary>
    /// Adds a tag to the specified model.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    /// <param name="tagName">
    /// The name of the tag to assign.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the tag was added; otherwise,
    /// <see langword="false"/> when the model already has the tag or has
    /// reached the maximum number of tags.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelIdentity"/> is null.
    /// </exception>
    public bool AddTag(
        ModelIdentity modelIdentity,
        string tagName) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        TagDefinition tag =
            tagCatalog.GetOrCreate(
                tagName);

        if (!modelTags.TryGetValue(
                modelIdentity.Value,
                out ModelTagCollection? collection)) {

            collection =
                new ModelTagCollection();

            modelTags.Add(
                modelIdentity.Value,
                collection);
        }

        bool added =
            collection.Add(
                tag);

        if (added) {
            Save();
        }

        return added;
    }

    /// <summary>
    /// Removes a tag from the specified model.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    /// <param name="tagName">
    /// The name of the tag to remove.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the tag was removed; otherwise,
    /// <see langword="false"/> when the model does not have the tag.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelIdentity"/> is null.
    /// </exception>
    public bool RemoveTag(
        ModelIdentity modelIdentity,
        string tagName) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        if (!modelTags.TryGetValue(
                modelIdentity.Value,
                out ModelTagCollection? collection)) {
            return false;
        }

        TagDefinition tag =
            new TagDefinition(
                tagName);

        bool removed =
            collection.Remove(
                tag);

        if (!removed) {
            return false;
        }

        // Empty model collections do not carry useful state and are therefore
        // removed from the in-memory index.
        if (collection.Count == 0) {
            modelTags.Remove(
                modelIdentity.Value);
        }

        Save();

        return true;
    }

    /// <summary>
    /// Gets the number of models currently using the specified tag.
    /// </summary>
    /// <param name="tagName">
    /// The name of the tag to count.
    /// </param>
    /// <returns>
    /// The number of models that currently use the tag.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tagName"/> is null, empty or whitespace.
    /// </exception>
    public int GetModelCountUsingTag(
        string tagName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tagName);

        TagDefinition tag =
            new TagDefinition(
                tagName);

        int modelCount =
            0;

        foreach (ModelTagCollection collection
                 in modelTags.Values) {

            if (collection.Contains(
                    tag)) {
                modelCount++;
            }
        }

        return modelCount;
    }

    /// <summary>
    /// Deletes a tag globally from the tag catalog and every model using it.
    /// </summary>
    /// <param name="tagName">
    /// The name of the tag to delete.
    /// </param>
    /// <returns>
    /// The number of models from which the tag was removed.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tagName"/> is null, empty or whitespace.
    /// </exception>
    /// <remarks>
    /// Models that become empty as a result of the deletion are removed from
    /// the model-tag index. The operation is persisted once after all affected
    /// data has been updated.
    /// </remarks>
    public int DeleteTag(
        string tagName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tagName);

        if (!tagCatalog.TryGet(
                tagName,
                out TagDefinition? tag) ||
            tag is null) {
            return 0;
        }

        int affectedModelCount =
            0;

        List<string> emptyModelIdentities =
            new List<string>();

        foreach (KeyValuePair<string, ModelTagCollection> entry
                 in modelTags) {

            ModelTagCollection collection =
                entry.Value;

            if (!collection.Contains(
                    tag)) {
                continue;
            }

            bool removed =
                collection.Remove(
                    tag);

            if (!removed) {
                continue;
            }

            affectedModelCount++;

            if (collection.Count == 0) {
                // Defer dictionary removal until after enumeration has completed
                // because the collection is currently being iterated.
                emptyModelIdentities.Add(
                    entry.Key);
            }
        }

        foreach (string modelIdentity
                 in emptyModelIdentities) {

            modelTags.Remove(
                modelIdentity);
        }

        tagCatalog.Remove(
            tag);

        Save();

        return affectedModelCount;
    }

    /// <summary>
    /// Serializes the current in-memory tag catalog and model metadata and
    /// delegates persistence to <see cref="TagPersistenceService"/>.
    /// </summary>
    public void Save() {
        TagStore store =
            new TagStore();

        foreach (TagDefinition tag
                 in tagCatalog.Tags) {
            store.Tags.Add(
                tag.Name);
        }

        // A model must be persisted when it has either tags or Favorite state.
        // Combining both sets ensures that Favorite-only models are not lost.
        HashSet<string> modelIdentities =
            new HashSet<string>(
                modelTags.Keys,
                StringComparer.Ordinal);

        modelIdentities.UnionWith(
            favoriteModelIdentities);

        foreach (string modelIdentity
                 in modelIdentities) {

            List<string> tags =
                modelTags.TryGetValue(
                    modelIdentity,
                    out ModelTagCollection? collection)
                    ? collection.Tags
                        .Select(
                            tag =>
                                tag.Name)
                        .ToList()
                    : new List<string>();

            store.Models.Add(
                new ModelTagStoreEntry(
                    modelIdentity,
                    tags) {
                    IsFavorite =
                        favoriteModelIdentities.Contains(
                            modelIdentity)
                });
        }

        persistenceService.Save(
            store);
    }

    /// <summary>
    /// Loads persisted tag and model metadata into the in-memory indexes.
    /// </summary>
    /// <remarks>
    /// Invalid or incomplete persisted model entries are ignored rather than
    /// preventing the remainder of the valid store from being loaded.
    /// </remarks>
    private void Load() {
        TagStore store =
            persistenceService.Load();

        foreach (string tagName
                 in store.Tags) {
            tagCatalog.GetOrCreate(
                tagName);
        }

        foreach (ModelTagStoreEntry model
                 in store.Models) {

            if (string.IsNullOrWhiteSpace(
                    model.ModelId)) {
                continue;
            }

            ModelIdentity modelIdentity =
                new ModelIdentity(
                    model.ModelId);

            if (model.IsFavorite) {
                favoriteModelIdentities.Add(
                    modelIdentity.Value);
            }

            ModelTagCollection collection =
                new ModelTagCollection();

            foreach (string tagName
                     in model.Tags) {

                if (!tagCatalog.TryGet(
                        tagName,
                        out TagDefinition? tag) ||
                    tag is null) {
                    continue;
                }

                collection.Add(
                    tag);
            }

            if (collection.Count > 0) {
                modelTags[modelIdentity.Value] =
                    collection;
            }
        }
    }
}