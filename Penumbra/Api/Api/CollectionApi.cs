using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;

namespace Penumbra.Api.Api;

public class CollectionApi(CollectionManager collections, ApiHelpers helpers) : IPenumbraApiCollection, IApiService
{
    public Dictionary<Guid, string> GetCollections()
        => collections.Storage.ToDictionary(c => c.Id, c => c.Name);

    public Dictionary<string, object?> GetChangedItemsForCollection(Guid collectionId)
    {
        try
        {
            if (!collections.Storage.ById(collectionId, out var collection))
                collection = ModCollection.Empty;

            if (collection.HasCache)
                return collection.ChangedItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item2);

            Penumbra.Log.Warning($"Collection {collectionId} does not exist or is not loaded.");
            return [];
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not obtain Changed Items for {collectionId}:\n{e}");
            throw;
        }
    }

    public (Guid Id, string Name)? GetCollection(ApiCollectionType type)
    {
        if (!Enum.IsDefined(type))
            return null;

        var collection = collections.Active.ByType((CollectionType)type);
        return collection == null ? null : (collection.Id, collection.Name);
    }

    internal (Guid Id, string Name)? GetCollection(byte type)
        => GetCollection((ApiCollectionType)type);

    public (bool ObjectValid, bool IndividualSet, (Guid Id, string Name) EffectiveCollection) GetCollectionForObject(int gameObjectIdx)
    {
        var id = helpers.AssociatedIdentifier(gameObjectIdx);
        if (!id.IsValid)
            return (false, false, (collections.Active.Default.Id, collections.Active.Default.Name));

        if (collections.Active.Individuals.TryGetValue(id, out var collection))
            return (true, true, (collection.Id, collection.Name));

        helpers.AssociatedCollection(gameObjectIdx, out collection);
        return (true, false, (collection.Id, collection.Name));
    }

    public Guid[] GetCollectionByName(string name)
        => collections.Storage.Where(c => string.Equals(name, c.Name, StringComparison.OrdinalIgnoreCase)).Select(c => c.Id).ToArray();

    public (PenumbraApiEc, (Guid Id, string Name)? OldCollection) SetCollection(ApiCollectionType type, Guid? collectionId,
        bool allowCreateNew, bool allowDelete)
    {
        if (!Enum.IsDefined(type))
            return (PenumbraApiEc.InvalidArgument, null);

        var oldCollection = collections.Active.ByType((CollectionType)type);
        var old           = oldCollection != null ? (oldCollection.Id, oldCollection.Name) : new ValueTuple<Guid, string>?();
        if (collectionId == null)
        {
            if (old == null)
                return (PenumbraApiEc.NothingChanged, old);

            if (!allowDelete || type is ApiCollectionType.Current or ApiCollectionType.Default or ApiCollectionType.Interface)
                return (PenumbraApiEc.AssignmentDeletionDisallowed, old);

            collections.Active.RemoveSpecialCollection((CollectionType)type);
            return (PenumbraApiEc.Success, old);
        }

        if (!collections.Storage.ById(collectionId.Value, out var collection))
            return (PenumbraApiEc.CollectionMissing, old);

        if (old == null)
        {
            if (!allowCreateNew)
                return (PenumbraApiEc.AssignmentCreationDisallowed, old);

            collections.Active.CreateSpecialCollection((CollectionType)type);
        }
        else if (old.Value.Item1 == collection.Id)
        {
            return (PenumbraApiEc.NothingChanged, old);
        }

        collections.Active.SetCollection(collection, (CollectionType)type);
        return (PenumbraApiEc.Success, old);
    }

    public (PenumbraApiEc, (Guid Id, string Name)? OldCollection) SetCollectionForObject(int gameObjectIdx, Guid? collectionId,
        bool allowCreateNew, bool allowDelete)
    {
        var id = helpers.AssociatedIdentifier(gameObjectIdx);
        if (!id.IsValid)
            return (PenumbraApiEc.InvalidIdentifier, (collections.Active.Default.Id, collections.Active.Default.Name));

        var oldCollection = collections.Active.Individuals.TryGetValue(id, out var c) ? c : null;
        var old           = oldCollection != null ? (oldCollection.Id, oldCollection.Name) : new ValueTuple<Guid, string>?();
        if (collectionId == null)
        {
            if (old == null)
                return (PenumbraApiEc.NothingChanged, old);

            if (!allowDelete)
                return (PenumbraApiEc.AssignmentDeletionDisallowed, old);

            var idx = collections.Active.Individuals.Index(id);
            collections.Active.RemoveIndividualCollection(idx);
            return (PenumbraApiEc.Success, old);
        }

        if (!collections.Storage.ById(collectionId.Value, out var collection))
            return (PenumbraApiEc.CollectionMissing, old);

        if (old == null)
        {
            if (!allowCreateNew)
                return (PenumbraApiEc.AssignmentCreationDisallowed, old);

            var ids = collections.Active.Individuals.GetGroup(id);
            collections.Active.CreateIndividualCollection(ids);
        }
        else if (old.Value.Item1 == collection.Id)
        {
            return (PenumbraApiEc.NothingChanged, old);
        }

        collections.Active.SetCollection(collection, CollectionType.Individual, collections.Active.Individuals.Index(id));
        return (PenumbraApiEc.Success, old);
    }
}
