// -----------------------------------------------------------------------------
// TagService.cs
//
// Provides the runtime business logic for BrickView's tag system.
//
// The service keeps tag data in memory so tag lookups never require disk I/O.
// Persistent storage is handled by TagPersistenceService.
//
// Responsibilities include:
// - Managing the reusable tag catalog.
// - Managing model-to-tag relationships.
// - Enforcing the maximum of three tags per model.
// - Normalizing tags through TagDefinition.
// - Persisting changes to the tag store.
// - Removing a tag globally from the tag catalog and every model that uses it.
//
// Global tag deletion is deliberately handled here rather than in the UI so
// the operation remains consistent regardless of where it is initiated.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagService {
    private readonly TagPersistenceService persistenceService;

    private readonly TagCatalog tagCatalog;

    private readonly Dictionary<string, ModelTagCollection> modelTags;

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

        if (!modelTags.Remove(
                modelIdentity.Value)) {
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

        if (!modelTags.TryGetValue(
                oldModelIdentity.Value,
                out ModelTagCollection? collection)) {
            return;
        }

        modelTags.Remove(
            oldModelIdentity.Value);

        modelTags[newModelIdentity.Value] =
            collection;

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

        foreach (KeyValuePair<string, ModelTagCollection> entry
                 in modelTags) {
            store.Models.Add(
                new ModelTagStoreEntry(
                    entry.Key,
                    entry.Value.Tags.Select(
                        tag => tag.Name)));
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

            ModelIdentity modelIdentity =
                new ModelIdentity(
                    model.ModelId);

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