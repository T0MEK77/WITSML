using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using PDS.WITSMLstudio.Store.Data.Security;

namespace PDS.WITSMLstudio.Store.Security
{
    /// <summary>Validates existing ACL data before creating its unique identity index.</summary>
    public static class AclRuleIndexManager
    {
        public const string IndexName = "UX_AclRules_Username_Resource_Type_Action";

        /// <summary>Returns duplicate ACL identity groups without changing collection data.</summary>
        public static IList<BsonDocument> FindDuplicateKeys(IMongoCollection<AclRule> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument
                        {
                            { "Username", "$Username" },
                            { "ResourceId", "$ResourceId" },
                            { "ResourceType", "$ResourceType" },
                            { "Action", "$Action" }
                        }
                    },
                    { "Count", new BsonDocument("$sum", 1) },
                    { "DocumentIds", new BsonDocument("$push", "$_id") }
                }),
                new BsonDocument("$match", new BsonDocument("Count", new BsonDocument("$gt", 1))),
                new BsonDocument("$sort", new BsonDocument("Count", -1))
            };

            return collection.Aggregate<BsonDocument>(pipeline).ToList();
        }

        /// <summary>Creates the unique ACL identity index only when existing data is conflict-free.</summary>
        public static void EnsureUniqueIndex(IMongoCollection<AclRule> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            var duplicates = FindDuplicateKeys(collection);
            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot create the unique ACL index because "
                    + duplicates.Count
                    + " duplicate key group(s) exist. Resolve them explicitly before retrying. "
                    + string.Join("; ", duplicates));
            }

            var keys = Builders<AclRule>.IndexKeys
                .Ascending(x => x.Username)
                .Ascending(x => x.ResourceId)
                .Ascending(x => x.ResourceType)
                .Ascending(x => x.Action);

            collection.Indexes.CreateOne(new CreateIndexModel<AclRule>(
                keys,
                new CreateIndexOptions { Unique = true, Name = IndexName }));
        }
    }
}
