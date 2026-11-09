//-----------------------------------------------------------------------
// WITSMLstudio Store
// Object-level access control policy model.
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace PDS.WITSMLstudio.Store.Data.Security
{
    /// <summary>Operations that can be granted on a WITSML resource.</summary>
    public enum AclAction
    {
        Read,
        Create,
        Update,
        Delete
    }

    /// <summary>The effect of a matching ACL rule.</summary>
    public enum AclEffect
    {
        Allow,
        Deny
    }

    /// <summary>The level at which an ACL rule is attached.</summary>
    public enum AclResourceType
    {
        Well,
        Wellbore,
        DataObject
    }

    /// <summary>
    /// A persisted ACL rule. ResourceId is the stable identifier at the selected
    /// hierarchy level; Inherit applies the rule to descendants.
    /// </summary>
    public sealed class AclRule
    {
        public string Username { get; set; }
        public string ResourceId { get; set; }
        public AclResourceType ResourceType { get; set; }
        public AclAction Action { get; set; }
        public AclEffect Effect { get; set; }
        public bool Inherit { get; set; }
    }

    /// <summary>A resource and its parent identifiers, from object to Well.</summary>
    public sealed class AclResource
    {
        public string Id { get; set; }
        public AclResourceType Type { get; set; }
    }

    /// <summary>Result of evaluating an object-level ACL.</summary>
    public sealed class AclDecision
    {
        public bool IsAclConfigured { get; internal set; }
        public bool IsAllowed { get; internal set; }
    }

    /// <summary>
    /// Evaluates ACL rules for a resource hierarchy. Explicit Deny always wins.
    /// If a hierarchy has ACL rules but none grants the requested action to the
    /// user, access is denied. An entirely unconfigured hierarchy is distinguishable
    /// so callers can preserve the legacy authorization behavior.
    /// </summary>
    public static class AclPolicyEvaluator
    {
        public static AclDecision Evaluate(
            string username,
            AclAction action,
            IEnumerable<AclResource> resourceHierarchy,
            IEnumerable<AclRule> rules)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("A username is required.", "username");

            if (resourceHierarchy == null)
                throw new ArgumentNullException("resourceHierarchy");

            if (rules == null)
                throw new ArgumentNullException("rules");

            var resources = resourceHierarchy
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .ToList();
            var allRules = rules.Where(x => x != null).ToList();

            var configured = allRules.Any(rule =>
                resources.Any(resource =>
                    string.Equals(resource.Id, rule.ResourceId, StringComparison.OrdinalIgnoreCase)
                    && resource.Type == rule.ResourceType));

            if (!configured)
                return new AclDecision { IsAclConfigured = false, IsAllowed = false };

            var applicable = allRules.Where(rule =>
                rule.Action == action
                && (string.Equals(rule.Username, username, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rule.Username, "*", StringComparison.Ordinal))
                && IsApplicable(rule, resources)).ToList();

            if (applicable.Any(rule => rule.Effect == AclEffect.Deny))
                return new AclDecision { IsAclConfigured = true, IsAllowed = false };

            return new AclDecision
            {
                IsAclConfigured = true,
                IsAllowed = applicable.Any(rule => rule.Effect == AclEffect.Allow)
            };
        }

        private static bool IsApplicable(AclRule rule, IList<AclResource> hierarchy)
        {
            for (var index = 0; index < hierarchy.Count; index++)
            {
                var resource = hierarchy[index];
                if (!string.Equals(resource.Id, rule.ResourceId, StringComparison.OrdinalIgnoreCase)
                    || resource.Type != rule.ResourceType)
                    continue;

                // A rule on the target itself always applies. Rules on parents
                // apply only when inheritance is explicitly enabled.
                return index == 0 || rule.Inherit;
            }

            return false;
        }
    }
}
