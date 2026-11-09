using System.Collections.Generic;

namespace PDS.WITSMLstudio.Store.Data.Security
{
    /// <summary>Persistence contract for object-level ACL rules.</summary>
    public interface IAclRuleStore
    {
        IList<AclRule> GetRules(IEnumerable<string> resourceIds);
        void Save(AclRule rule);
        void Delete(string username, string resourceId, AclResourceType resourceType, AclAction action);
    }
}
