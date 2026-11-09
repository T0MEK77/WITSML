//-----------------------------------------------------------------------
// WITSMLstudio Store
// Integration tests for MongoDB ACL rule persistence.
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using PDS.WITSMLstudio.Store.Data;
using PDS.WITSMLstudio.Store.Data.Security;
using PDS.WITSMLstudio.Store.Security;

namespace PDS.WITSMLstudio.Store.Security
{
    [TestClass]
    public class AclRuleStoreTests
    {
        private IDatabaseProvider _databaseProvider;
        private MongoDbAclRuleStore _store;
        private string _resourceId;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestSetUp()
        {
            var devKit = new DevKit141Aspect(TestContext);
            _databaseProvider = devKit.Container.Resolve<IDatabaseProvider>();
            _store = new MongoDbAclRuleStore(_databaseProvider);
            _resourceId = "acl-test-" + Guid.NewGuid().ToString("N");
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            if (_databaseProvider == null || string.IsNullOrEmpty(_resourceId))
                return;

            _databaseProvider.GetDatabase()
                .GetCollection<AclRule>("AclRules")
                .DeleteMany(Builders<AclRule>.Filter.Eq(x => x.ResourceId, _resourceId));
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MongoDbAclRuleStore_Save_Null_Rule_Throws_Validation_Exception()
        {
            _store.Save(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MongoDbAclRuleStore_Save_Blank_Username_Throws_Validation_Exception()
        {
            _store.Save(new AclRule
            {
                Username = " ",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.Well,
                Action = AclAction.Read,
                Effect = AclEffect.Allow
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MongoDbAclRuleStore_Save_Blank_Resource_Id_Throws_Validation_Exception()
        {
            _store.Save(new AclRule
            {
                Username = "acl-test-user",
                ResourceId = " ",
                ResourceType = AclResourceType.Well,
                Action = AclAction.Read,
                Effect = AclEffect.Allow
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MongoDbAclRuleStore_GetRules_Null_Resource_Ids_Throws_Validation_Exception()
        {
            _store.GetRules(null);
        }

        [TestMethod]
        public void MongoDbAclRuleStore_Concurrent_Save_Does_Not_Create_Duplicate_Rules()
        {
            System.Threading.Tasks.Parallel.For(0, 8, index =>
            {
                _store.Save(new AclRule
                {
                    Username = "acl-concurrent-user",
                    ResourceId = _resourceId,
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = index % 2 == 0 ? AclEffect.Allow : AclEffect.Deny,
                    Inherit = index % 2 == 0
                });
            });

            var result = _store.GetRules(new[] { _resourceId });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("acl-concurrent-user", result[0].Username);
        }

        [TestMethod]
        public void MongoDbAclRuleStore_Save_And_GetRules_Persist_Rule()
        {
            var rule = new AclRule
            {
                Username = "acl-test-user",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.Well,
                Action = AclAction.Read,
                Effect = AclEffect.Allow,
                Inherit = true
            };

            _store.Save(rule);

            var result = _store.GetRules(new[] { _resourceId });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(rule.Username, result[0].Username);
            Assert.AreEqual(rule.ResourceId, result[0].ResourceId);
            Assert.AreEqual(rule.ResourceType, result[0].ResourceType);
            Assert.AreEqual(rule.Action, result[0].Action);
            Assert.AreEqual(rule.Effect, result[0].Effect);
            Assert.IsTrue(result[0].Inherit);
        }

        [TestMethod]
        public void MongoDbAclRuleStore_Save_Updates_Existing_Composite_Key()
        {
            var rule = new AclRule
            {
                Username = "acl-test-user",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.Wellbore,
                Action = AclAction.Update,
                Effect = AclEffect.Allow,
                Inherit = false
            };

            _store.Save(rule);
            rule.Effect = AclEffect.Deny;
            rule.Inherit = true;
            _store.Save(rule);

            var result = _store.GetRules(new[] { _resourceId });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(AclEffect.Deny, result[0].Effect);
            Assert.IsTrue(result[0].Inherit);
        }

        [TestMethod]
        public void MongoDbAclRuleStore_GetRules_Ignores_Blank_And_Duplicate_Resource_Ids()
        {
            _store.Save(new AclRule
            {
                Username = "acl-test-user",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.DataObject,
                Action = AclAction.Delete,
                Effect = AclEffect.Deny
            });

            var result = _store.GetRules(new[] { _resourceId, null, string.Empty, " ", _resourceId });
            var empty = _store.GetRules(new string[0]);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, empty.Count);
        }

        [TestMethod]
        public void MongoDbAclRuleStore_Delete_Removes_Only_Matching_Action()
        {
            _store.Save(new AclRule
            {
                Username = "acl-test-user",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.Well,
                Action = AclAction.Read,
                Effect = AclEffect.Allow
            });
            _store.Save(new AclRule
            {
                Username = "acl-test-user",
                ResourceId = _resourceId,
                ResourceType = AclResourceType.Well,
                Action = AclAction.Update,
                Effect = AclEffect.Allow
            });

            _store.Delete("acl-test-user", _resourceId, AclResourceType.Well, AclAction.Read);

            var result = _store.GetRules(new[] { _resourceId });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(AclAction.Update, result[0].Action);
        }
    }
}
