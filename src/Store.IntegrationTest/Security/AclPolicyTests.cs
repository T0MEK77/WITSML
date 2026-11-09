using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PDS.WITSMLstudio.Store.Data.Security;

namespace PDS.WITSMLstudio.Store.Security
{
    [TestClass]
    public class AclPolicyTests
    {
        private static IList<AclResource> Hierarchy()
        {
            return new List<AclResource>
            {
                new AclResource { Id = "object-1", Type = AclResourceType.DataObject },
                new AclResource { Id = "wellbore-1", Type = AclResourceType.Wellbore },
                new AclResource { Id = "well-1", Type = AclResourceType.Well }
            };
        }

        [TestMethod]
        public void Evaluate_Allows_Inherited_Well_Rule()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = AclEffect.Allow,
                    Inherit = true
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Read, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsTrue(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Deny_Takes_Precedence_Over_Inherited_Allow()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = AclEffect.Allow,
                    Inherit = true
                },
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "object-1",
                    ResourceType = AclResourceType.DataObject,
                    Action = AclAction.Read,
                    Effect = AclEffect.Deny
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Read, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Does_Not_Scope_Descendants_When_Parent_Rule_Does_Not_Inherit()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = AclEffect.Allow,
                    Inherit = false
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Read, Hierarchy(), rules);

            Assert.IsFalse(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Allows_Inherited_Wellbore_Rule_For_DataObject()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "wellbore-1",
                    ResourceType = AclResourceType.Wellbore,
                    Action = AclAction.Update,
                    Effect = AclEffect.Allow,
                    Inherit = true
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Update, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsTrue(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Denies_When_Well_Ancestor_Explicitly_Denies()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "*",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Delete,
                    Effect = AclEffect.Deny,
                    Inherit = true
                },
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "object-1",
                    ResourceType = AclResourceType.DataObject,
                    Action = AclAction.Delete,
                    Effect = AclEffect.Allow
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Delete, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Denies_By_Default_Inside_Configured_Scope()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "bob",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = AclEffect.Allow,
                    Inherit = true
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Read, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }
    }
}
