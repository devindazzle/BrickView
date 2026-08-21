// -----------------------------------------------------------------------------
// TagService.cs
//
// Provides the runtime business logic for BrickView's tag system and
// model-level metadata.
//
// The service keeps tag data and Favorite state in memory so lookups never
// require disk I/O. Persistent storage is handled by TagPersistenceService.
//
// Responsibilities include:
// - Managing the reusable tag catalog.
// - Managing model-to-tag relationships.
// - Managing model Favorite state.
// - Enforcing the maximum of three tags per model.
// - Normalizing tags through TagDefinition.
// - Persisting model metadata to the tag store.
// - Removing a tag globally from the tag catalog and every model that uses it.
//
// Favorites are model metadata and are deliberately kept separate from tags.
// Both are persisted using the same stable ModelIdentity-based model entry.
//
// Global tag deletion is deliberately handled here rather than in the UI so
// the operation remains consistent regardless of where it is initiated.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagService {
    private readonly TagPersistenceService persistenceService;

    private readonly TagCatalog tagCatalog;

    private readonly Dictionary<string, ModelTagCollection> modelTags;

    private readonly HashSet<string> favoriteModelIdentities;

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

    public IReadOnlyCollection<TagDefinition> GetAllTags() {
        return tagCatalog.Tags;
    }

    public bool TryGetTag(
        string tagName,
        out TagDefinition? tag) {
        return tagCatalog.TryGet(
            tagName,
            out tag);
    }

    public TagDefinition GetOrCreateTag(
        string tagName) {
        TagDefinition tag =
            tagCatalog.GetOrCreate(
                tagName);

        Save();

        return tag;
    }

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

    public bool IsFavorite(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        return favoriteModelIdentities.Contains(
            modelIdentity.Value);
    }

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

        Save();

        return true;
    }

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

        if (collection.Count == 0) {
            modelTags.Remove(
                modelIdentity.Value);
        }

        Save();

        return true;
    }

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

    public void RemoveModel(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        bool removedTags =
            modelTags.Remove(
                modelIdentity.Value);

        bool removedFavorite =
            favoriteModelIdentities.Remove(
                modelIdentity.Value);

        if (!removedTags &&
            !removedFavorite) {
            return;
        }

        Save();
    }

    public void UpdateModelIdentity(
        ModelIdentity oldModelIdentity,
        ModelIdentity newModelIdentity) {
        ArgumentNullException.ThrowIfNull(
            oldModelIdentity);

        ArgumentNullException.ThrowIfNull(
            newModelIdentity);

        if (string.Equals(
                oldModelIdentity.Value,
                newModelIdentity.Value,
                StringComparison.Ordinal)) {
            return;
        }

        bool changed =
            false;

        if (modelTags.TryGetValue(
                oldModelIdentity.Value,
                out ModelTagCollection? collection)) {

            modelTags.Remove(
                oldModelIdentity.Value);

            modelTags[newModelIdentity.Value] =
                collection;

            changed =
                true;
        }

        if (favoriteModelIdentities.Remove(
                oldModelIdentity.Value)) {

            favoriteModelIdentities.Add(
                newModelIdentity.Value);

            changed =
                true;
        }

        if (!changed) {
            return;
        }

        Save();
    }

    public void Save() {
        TagStore store =
            new TagStore();

        foreach (TagDefinition tag
                 in tagCatalog.Tags) {
            store.Tags.Add(
                tag.Name);
        }

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