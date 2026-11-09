using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PDS.WITSMLstudio.Store.Data.Security;
using PDS.WITSMLstudio.Store.Configuration;

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

        [TestMethod]
        public void Evaluate_Create_Uses_Immediate_Parent_Without_Inheritance()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "wellbore-1",
                    ResourceType = AclResourceType.Wellbore,
                    Action = AclAction.Create,
                    Effect = AclEffect.Allow,
                    Inherit = false
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Create, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsTrue(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Create_Does_Not_Use_NonInherited_Grandparent_Rule()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "alice",
                    ResourceId = "well-1",
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Create,
                    Effect = AclEffect.Allow,
                    Inherit = false
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Create, Hierarchy(), rules);

            Assert.IsFalse(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }

        [TestMethod]
        public void Evaluate_Create_Denies_By_Default_When_Immediate_Parent_Is_Acl_Scoped()
        {
            var rules = new[]
            {
                new AclRule
                {
                    Username = "bob",
                    ResourceId = "wellbore-1",
                    ResourceType = AclResourceType.Wellbore,
                    Action = AclAction.Read,
                    Effect = AclEffect.Allow,
                    Inherit = false
                }
            };

            var result = AclPolicyEvaluator.Evaluate("alice", AclAction.Create, Hierarchy(), rules);

            Assert.IsTrue(result.IsAclConfigured);
            Assert.IsFalse(result.IsAllowed);
        }

        [TestMethod]
        public void UserAuthorizationProvider_Denies_When_Acl_Rule_Store_Is_Missing()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = string.Empty;

                var provider = new UserAuthorizationProvider { AclRuleStore = null };

                var exception = Assert.ThrowsException<WitsmlException>(() =>
                    provider.CheckObjectAccess("alice", AclAction.Read, Hierarchy()));

                Assert.AreEqual(ErrorCodes.InsufficientOperationRights, exception.ErrorCode);
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        [TestMethod]
        public void UserAuthorizationProvider_Denies_When_Username_Is_Empty()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = string.Empty;

                var provider = new UserAuthorizationProvider
                {
                    AclRuleStore = new TestAclRuleStore()
                };

                var exception = Assert.ThrowsException<WitsmlException>(() =>
                    provider.CheckObjectAccess(" ", AclAction.Read, Hierarchy()));

                Assert.AreEqual(ErrorCodes.InsufficientOperationRights, exception.ErrorCode);
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        [TestMethod]
        public void UserAuthorizationProvider_Allows_Configured_Administrator_Without_Acl_Rules()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = "ops-admin;SECURITY-ADMIN";

                var provider = new UserAuthorizationProvider { AclRuleStore = null };

                provider.CheckObjectAccess("security-admin", AclAction.Delete, Hierarchy());
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        [TestMethod]
        public void UserAuthorizationProvider_Preserves_Legacy_Access_For_Unconfigured_Hierarchy()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = string.Empty;

                var provider = new UserAuthorizationProvider
                {
                    AclRuleStore = new TestAclRuleStore()
                };

                provider.CheckObjectAccess("alice", AclAction.Read, Hierarchy());
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        [TestMethod]
        public void UserAuthorizationProvider_Denies_User_Without_Allow_In_Configured_Scope()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = string.Empty;

                var provider = new UserAuthorizationProvider
                {
                    AclRuleStore = new TestAclRuleStore(new AclRule
                    {
                        Username = "bob",
                        ResourceId = "well-1",
                        ResourceType = AclResourceType.Well,
                        Action = AclAction.Read,
                        Effect = AclEffect.Allow,
                        Inherit = true
                    })
                };

                var exception = Assert.ThrowsException<WitsmlException>(() =>
                    provider.CheckObjectAccess("alice", AclAction.Read, Hierarchy()));

                Assert.AreEqual(ErrorCodes.InsufficientOperationRights, exception.ErrorCode);
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        [TestMethod]
        public void UserAuthorizationProvider_Allows_User_With_Applicable_Allow_Rule()
        {
            var originalEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                WitsmlSettings.AclAdministratorUsers = string.Empty;

                var provider = new UserAuthorizationProvider
                {
                    AclRuleStore = new TestAclRuleStore(new AclRule
                    {
                        Username = "alice",
                        ResourceId = "well-1",
                        ResourceType = AclResourceType.Well,
                        Action = AclAction.Read,
                        Effect = AclEffect.Allow,
                        Inherit = true
                    })
                };

                provider.CheckObjectAccess("alice", AclAction.Read, Hierarchy());
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = originalEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
            }
        }

        private sealed class TestAclRuleStore : IAclRuleStore
        {
            private readonly IList<AclRule> _rules;

            public TestAclRuleStore(params AclRule[] rules)
            {
                _rules = new List<AclRule>(rules ?? new AclRule[0]);
            }

            public IList<AclRule> GetRules(IEnumerable<string> resourceIds)
            {
                return _rules;
            }

            public void Save(AclRule rule)
            {
                _rules.Add(rule);
            }

            public void Delete(string username, string resourceId, AclResourceType resourceType, AclAction action)
            {
                var rule = _rules.FirstOrDefault(x =>
                    x.Username == username &&
                    x.ResourceId == resourceId &&
                    x.ResourceType == resourceType &&
                    x.Action == action);

                if (rule != null)
                    _rules.Remove(rule);
            }
        }
    }
}
