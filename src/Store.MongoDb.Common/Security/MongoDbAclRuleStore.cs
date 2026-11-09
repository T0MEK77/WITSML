using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.Composition;
using MongoDB.Driver;
using PDS.WITSMLstudio.Store.Data;
using PDS.WITSMLstudio.Store.Data.Security;

namespace PDS.WITSMLstudio.Store.Security
{
    /// <summary>Stores ACL rules in the configured WITSML MongoDB database.</summary>
    [Export(typeof(IAclRuleStore))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class MongoDbAclRuleStore : IAclRuleStore
    {
        private const string CollectionName = "AclRules";
        private readonly IDatabaseProvider _databaseProvider;

        [ImportingConstructor]
        public MongoDbAclRuleStore(IDatabaseProvider databaseProvider)
        {
            _databaseProvider = databaseProvider;
        }

        private IMongoCollection<AclRule> Collection
        {
            get { return _databaseProvider.GetDatabase().GetCollection<AclRule>(CollectionName); }
        }

        public IList<AclRule> GetRules(IEnumerable<string> resourceIds)
        {
            if (resourceIds == null) throw new ArgumentNullException("resourceIds");
            var ids = resourceIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (ids.Count == 0) return new List<AclRule>();
            return Collection.Find(Builders<AclRule>.Filter.In(x => x.ResourceId, ids)).ToList();
        }

        public void Save(AclRule rule)
        {
            if (rule == null) throw new ArgumentNullException("rule");
            if (string.IsNullOrWhiteSpace(rule.Username) || string.IsNullOrWhiteSpace(rule.ResourceId))
                throw new ArgumentException("Username and ResourceId are required.", "rule");

            var filter = Builders<AclRule>.Filter.And(
                Builders<AclRule>.Filter.Eq(x => x.Username, rule.Username),
                Builders<AclRule>.Filter.Eq(x => x.ResourceId, rule.ResourceId),
                Builders<AclRule>.Filter.Eq(x => x.ResourceType, rule.ResourceType),
                Builders<AclRule>.Filter.Eq(x => x.Action, rule.Action));
            Collection.ReplaceOne(filter, rule, new ReplaceOptions { IsUpsert = true });
        }

        public void Delete(string username, string resourceId, AclResourceType resourceType, AclAction action)
        {
            var filter = Builders<AclRule>.Filter.And(
                Builders<AclRule>.Filter.Eq(x => x.Username, username),
                Builders<AclRule>.Filter.Eq(x => x.ResourceId, resourceId),
                Builders<AclRule>.Filter.Eq(x => x.ResourceType, resourceType),
                Builders<AclRule>.Filter.Eq(x => x.Action, action));
            Collection.DeleteOne(filter);
        }
    }
}
