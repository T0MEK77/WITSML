using System.Collections.Generic;

namespace PDS.WITSMLstudio.Store.Data.Security
{
    /// <summary>Persistence contract for object-level ACL rules.</summary>
    public interface IAclRuleStore
    {
        /// <summary>Loads rules attached to any of the supplied resource identifiers.</summary>
        /// <param name="resourceIds">Stable identifiers in the target resource hierarchy.</param>
        /// <returns>The matching ACL rules.</returns>
        IList<AclRule> GetRules(IEnumerable<string> resourceIds);

        /// <summary>Creates or updates an ACL rule.</summary>
        /// <param name="rule">The rule to persist.</param>
        void Save(AclRule rule);

        /// <summary>Deletes the rule for the specified principal, resource, and action.</summary>
        /// <param name="username">The principal whose rule is removed.</param>
        /// <param name="resourceId">The stable resource identifier.</param>
        /// <param name="resourceType">The hierarchy level of the resource.</param>
        /// <param name="action">The governed operation.</param>
        void Delete(string username, string resourceId, AclResourceType resourceType, AclAction action);
    }
}
