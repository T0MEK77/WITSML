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
        /// <summary>Read an object or enumerate it in a result set.</summary>
        Read,
        /// <summary>Create a new object beneath the resource.</summary>
        Create,
        /// <summary>Modify an existing object.</summary>
        Update,
        /// <summary>Delete an existing object.</summary>
        Delete
    }

    /// <summary>The effect of a matching ACL rule.</summary>
    public enum AclEffect
    {
        /// <summary>Grant the specified action.</summary>
        Allow,
        /// <summary>Explicitly reject the specified action.</summary>
        Deny
    }

    /// <summary>The level at which an ACL rule is attached.</summary>
    public enum AclResourceType
    {
        /// <summary>A WITSML Well resource.</summary>
        Well,
        /// <summary>A WITSML Wellbore resource.</summary>
        Wellbore,
        /// <summary>A data object below a Wellbore.</summary>
        DataObject
    }

    /// <summary>
    /// A persisted ACL rule. ResourceId is the stable identifier at the selected
    /// hierarchy level; Inherit applies the rule to descendants.
    /// </summary>
    public sealed class AclRule
    {
        /// <summary>Gets or sets the authenticated username or wildcard '*'.</summary>
        public string Username { get; set; }
        /// <summary>Gets or sets the stable identifier of the target resource.</summary>
        public string ResourceId { get; set; }
        /// <summary>Gets or sets the hierarchy level of the target resource.</summary>
        public AclResourceType ResourceType { get; set; }
        /// <summary>Gets or sets the operation governed by this rule.</summary>
        public AclAction Action { get; set; }
        /// <summary>Gets or sets whether the rule allows or denies the operation.</summary>
        public AclEffect Effect { get; set; }
        /// <summary>Gets or sets whether a rule on a parent also applies to descendants.</summary>
        public bool Inherit { get; set; }
    }

    /// <summary>A resource and its parent identifiers, from object to Well.</summary>
    public sealed class AclResource
    {
        /// <summary>Gets or sets the stable resource identifier.</summary>
        public string Id { get; set; }
        /// <summary>Gets or sets the hierarchy level of this resource.</summary>
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
        /// <summary>Gets a value indicating whether the hierarchy contains an applicable ACL scope.</summary>
        public bool IsAclConfigured { get; internal set; }
        /// <summary>Gets a value indicating whether the requested operation is allowed.</summary>
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
        /// <summary>Evaluates matching rules for a user, operation, and resource hierarchy.</summary>
        /// <param name="username">The authenticated username.</param>
        /// <param name="action">The requested operation.</param>
        /// <param name="resourceHierarchy">Resources ordered from target to ancestors.</param>
        /// <param name="rules">Candidate rules loaded for the hierarchy.</param>
        /// <returns>The ACL decision, including whether ACL scope is configured.</returns>
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
            var configured = allRules.Any(rule => IsApplicable(rule, resources, action));

            if (!configured)
                return new AclDecision { IsAclConfigured = false, IsAllowed = false };

            var applicable = allRules.Where(rule =>
                rule.Action == action
                && (string.Equals(rule.Username, username, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rule.Username, "*", StringComparison.Ordinal))
                && IsApplicable(rule, resources, action)).ToList();

            if (applicable.Any(rule => rule.Effect == AclEffect.Deny))
                return new AclDecision { IsAclConfigured = true, IsAllowed = false };

            return new AclDecision
            {
                IsAclConfigured = true,
                IsAllowed = applicable.Any(rule => rule.Effect == AclEffect.Allow)
            };
        }

        private static bool IsApplicable(AclRule rule, IList<AclResource> hierarchy, AclAction action)
        {
            for (var index = 0; index < hierarchy.Count; index++)
            {
                var resource = hierarchy[index];
                if (!string.Equals(resource.Id, rule.ResourceId, StringComparison.OrdinalIgnoreCase)
                    || resource.Type != rule.ResourceType)
                    continue;

                // A rule on the target itself always applies. Rules on parents
                // apply only when inheritance is explicitly enabled.
                // Create is authorized against the immediate parent even when that
                // parent's rule is not inherited for ordinary child operations.
                return index == 0 || (action == AclAction.Create && index == 1) || rule.Inherit;
            }

            return false;
        }
    }
}
