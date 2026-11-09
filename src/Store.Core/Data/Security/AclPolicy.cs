//-----------------------------------------------------------------------
// WITSMLstudio Store
// Object-level access control policy model.
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Energistics.Etp.Common.Datatypes;

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

    /// <summary>Builds the ACL hierarchy from a canonical ETP URI.</summary>
    public static class AclResourceHierarchy
    {
        /// <summary>
        /// Returns resources ordered from the requested object to its Well ancestor.
        /// </summary>
        public static IList<AclResource> FromUri(EtpUri uri)
        {
            if (uri == null) throw new ArgumentNullException("uri");

            var resources = new List<AclResource>();
            var current = uri;

            while (!current.IsBaseUri)
            {
                if (!string.IsNullOrWhiteSpace(current.ObjectId))
                {
                    var objectType = current.ObjectType ?? string.Empty;
                    var resourceType = string.Equals(objectType, "Wellbore", StringComparison.OrdinalIgnoreCase)
                        ? AclResourceType.Wellbore
                        : string.Equals(objectType, "Well", StringComparison.OrdinalIgnoreCase)
                            ? AclResourceType.Well
                            : AclResourceType.DataObject;

                    resources.Add(new AclResource { Id = current.ObjectId, Type = resourceType });
                }

                current = current.Parent;
            }

            return resources;
        }
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

            // A parent rule scopes descendants only when inheritance is enabled.
            // A rule attached directly to the requested resource always scopes it.
            var configured = allRules.Any(rule => IsApplicable(rule, resources));

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
