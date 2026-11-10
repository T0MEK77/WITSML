//----------------------------------------------------------------------- 
// PDS WITSMLstudio Store, 2018.3
//
// Copyright 2018 PDS Americas LLC
// 
// Licensed under the PDS Open Source WITSML Product License Agreement (the
// "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//     http://www.pds.group/WITSMLstudio/OpenSource/ProductLicenseAgreement
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Energistics.Etp;
using Energistics.Etp.Common;
using Energistics.Etp.Common.Protocol.Core;
using Energistics.Etp.v11;
using Energistics.Etp.v11.Protocol.Core;
using Etp12CoreClient = Energistics.Etp.v12.Protocol.Core.ICoreClient;
using Etp12OpenSession = Energistics.Etp.v12.Protocol.Core.OpenSession;
using Energistics.Etp.v11.Protocol.GrowingObject;
using GrowingObject12Customer = Energistics.Etp.v12.Protocol.GrowingObject.IGrowingObjectCustomer;
using PDS.WITSMLstudio.Store.Configuration;
using PDS.WITSMLstudio.Store.Data.Security;
using Energistics.DataAccess.WITSML131;
using Energistics.DataAccess.WITSML131.ReferenceData;
using Energistics.Etp.v11.Protocol.Store;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PDS.WITSMLstudio.Compatibility;

namespace PDS.WITSMLstudio.Store.Data.Logs
{
    /// <summary>
    /// Log131EtpTests
    /// </summary>
    public partial class Log131EtpTests
    {
        protected override void OnTestSetUp()
        {
            base.OnTestSetUp();

            // Select the ETP version before EtpSetUp creates the WebSocket client
            // and registers version-specific server protocol handlers.
            TestSettings.EtpVersion = TestContext.TestName.StartsWith("Log131_Etp12", System.StringComparison.Ordinal)
                ? "1.2"
                : "1.1";
        }

        private async Task RequestEtp12SessionAndAssert(IEtpClient client)
        {
            var onOpenSession = HandleAsync<Etp12OpenSession>(
                x => client.Handler<Etp12CoreClient>().OnOpenSession += x);

            Assert.IsTrue(await client.OpenAsyncWithTimeout());
            var openArgs = await onOpenSession;

            Assert.IsNotNull(openArgs?.Message?.SessionId);
            System.Guid sessionId;
            Assert.IsTrue(System.Guid.TryParse(openArgs.Message.SessionId, out sessionId));

            foreach (var protocol in openArgs.Message.SupportedProtocols)
            {
                var version = protocol.ProtocolVersion;
                Assert.AreEqual("1.2", $"{version.Major}.{version.Minor}");
            }

            Assert.IsTrue(openArgs.Message.SupportedProtocols.Any(
                x => x.Protocol == (int)Protocols.GrowingObject));
        }
        [TestMethod]
        public async Task Log131_GrowingObjectGet_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Read,
                handler => handler.GrowingObjectGet(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectGet_Allows_Wildcard_Read_Allow()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var allowRule = new AclRule
            {
                Username = "*",
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = AclAction.Read,
                Effect = AclEffect.Allow
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                aclRuleStore.Save(allowRule);
                await RequestSessionAndAssert(10, Protocols.ChannelStreaming, Protocols.Discovery, Protocols.Store, Protocols.GrowingObject, Protocols.StoreNotification);

                var handler = _client.Handler<IGrowingObjectCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                handler.GrowingObjectGet(Log.GetUri().ToString(), "missing-part");

                var response = await onProtocolException;
                Assert.IsNotNull(response?.Message);
                Assert.AreEqual((int)Energistics.Etp.EtpErrorCodes.NotFound, response.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete("*", Log.Uid, AclResourceType.DataObject, AclAction.Read);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public async Task Log131_Etp12GetPart_Allows_Wildcard_Read_Allow()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var allowRule = new AclRule
            {
                Username = "*",
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = AclAction.Read,
                Effect = AclEffect.Allow
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                aclRuleStore.Save(allowRule);
                await RequestEtp12SessionAndAssert(_client);

                var handler = _client.Handler<GrowingObject12Customer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                handler.GetPart(Log.GetUri().ToString(), "missing-part");

                var response = await onProtocolException;
                Assert.IsNotNull(response?.Message);
                Assert.AreEqual((int)Energistics.Etp.EtpErrorCodes.NotFound, response.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete("*", Log.Uid, AclResourceType.DataObject, AclAction.Read);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public async Task Log131_GrowingObjectGetRange_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Read,
                handler => handler.GrowingObjectGetRange(Log.GetUri().ToString(), 0L, 10L, string.Empty, string.Empty));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectPut_Denies_Explicit_Update_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Update,
                handler => handler.GrowingObjectPut(Log.GetUri().ToString(), "text/xml", new byte[0]));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectDelete_Denies_Explicit_Delete_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Delete,
                handler => handler.GrowingObjectDelete(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectDeleteRange_Denies_Explicit_Delete_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Delete,
                handler => ((AclGrowingObjectCustomerHandler)handler).GrowingObjectDeleteRangeWithAclFields(Log.GetUri().ToString(), 0L, 10L, string.Empty, string.Empty));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectGet_Rechecks_Read_Acl_After_Session_Start()
        {
            await AssertGrowingObjectDeniedAfterSessionAsync(AclAction.Read,
                handler => handler.GrowingObjectGet(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_Etp12GetPart_Rechecks_Read_Acl_After_Session_Start()
        {
            await AssertGrowingObject12DeniedAfterSessionAsync(AclAction.Read,
                handler => handler.GetPart(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_Etp11StoreNotification_Denies_Subscription_With_Explicit_Read_Deny()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;
            var denyRule = new AclRule
            {
                Username = username,
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = AclAction.Read,
                Effect = AclEffect.Deny
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestSessionAndAssert(10, Protocols.ChannelStreaming, Protocols.Discovery, Protocols.Store, Protocols.GrowingObject, Protocols.StoreNotification);
                aclRuleStore.Save(denyRule);

                var handler = _client.Handler<Energistics.Etp.v11.Protocol.StoreNotification.IStoreNotificationCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                handler.NotificationRequest(new Energistics.Etp.v11.Datatypes.Object.NotificationRequestRecord
                {
                    IncludeObjectData = true,
                    ObjectTypes = new List<string>(),
                    StartTime = (long)((System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds * 1000L),
                    Uri = Log.GetUri().ToString(),
                    Uuid = System.Guid.NewGuid().ToString()
                });

                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, AclAction.Read);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public async Task Log131_Etp12StoreNotification_Denies_Subscription_With_Explicit_Read_Deny()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;
            var denyRule = new AclRule
            {
                Username = username,
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = AclAction.Read,
                Effect = AclEffect.Deny
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestEtp12SessionAndAssert(_client);
                aclRuleStore.Save(denyRule);

                var handler = _client.Handler<Energistics.Etp.v12.Protocol.StoreNotification.IStoreNotificationCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                handler.SubscribeNotification(new Energistics.Etp.v12.Datatypes.Object.SubscriptionInfo
                {
                    Context = new Energistics.Etp.v12.Datatypes.Object.ContextInfo
                    {
                        Uri = Log.GetUri().ToString(),
                        Depth = 0,
                        ContentTypes = new List<string>()
                    },
                    Scope = Energistics.Etp.v12.Datatypes.Object.ContextScopeKind.self,
                    IncludeObjectData = true,
                    RequestUuid = System.Guid.NewGuid().ToUuid()
                });

                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, AclAction.Read);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public void Log131_Etp11StoreNotification_OwnerRegistry_Denies_Other_User()
        {
            AssertOtherUserCannotManageSubscription("ETP11:" + System.Guid.NewGuid());
        }

        [TestMethod]
        public void Log131_Etp12StoreNotification_OwnerRegistry_Denies_Other_User()
        {
            AssertOtherUserCannotManageSubscription("ETP12:" + System.Guid.NewGuid());
        }

        private static void AssertOtherUserCannotManageSubscription(string registryKey)
        {
            const string owner = "acl-test-user";
            const string otherUser = "acl-other-user";

            try
            {
                Assert.IsTrue(
                    PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.TryRegister(registryKey, owner));

                string registeredOwner;
                Assert.IsTrue(
                    PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.TryGetOwner(registryKey, out registeredOwner));
                Assert.AreEqual(owner, registeredOwner);

                // A different user must not be able to claim an existing subscription identifier.
                Assert.IsFalse(
                    PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.TryRegister(registryKey, otherUser));

                // Exercise the exact owner guard used by both ETP 1.1 CancelNotification
                // and ETP 1.2 UnsubscribeNotification handlers.
                var denied = false;
                try
                {
                    PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.EnsureOwner(registeredOwner, otherUser);
                }
                catch (PDS.WITSMLstudio.WitsmlException exception)
                {
                    denied = true;
                    Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, (int)exception.ErrorCode);
                }

                Assert.IsTrue(denied, "A user other than the subscription owner must be denied.");
                PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.EnsureOwner(registeredOwner, owner);
            }
            finally
            {
                PDS.WITSMLstudio.Store.Providers.StoreNotification.StoreNotificationSubscriptionOwnerRegistry.Remove(registryKey, owner);
            }
        }

        private async Task AssertGrowingObjectDeniedAfterSessionAsync(AclAction action, System.Action<IGrowingObjectCustomer> sendRequest)
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;
            var denyRule = new AclRule { Username = username, ResourceId = Log.Uid, ResourceType = AclResourceType.DataObject, Action = action, Effect = AclEffect.Deny };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestSessionAndAssert(10, Protocols.ChannelStreaming, Protocols.Discovery, Protocols.Store, Protocols.GrowingObject, Protocols.StoreNotification);
                aclRuleStore.Save(denyRule);

                var handler = _client.Handler<IGrowingObjectCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                sendRequest(handler);
                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, action);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        private async Task AssertGrowingObject12DeniedAfterSessionAsync(AclAction action, System.Action<GrowingObject12Customer> sendRequest)
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;
            var denyRule = new AclRule { Username = username, ResourceId = Log.Uid, ResourceType = AclResourceType.DataObject, Action = action, Effect = AclEffect.Deny };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestEtp12SessionAndAssert(_client);
                aclRuleStore.Save(denyRule);

                var handler = _client.Handler<GrowingObject12Customer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                sendRequest(handler);
                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, action);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        private async Task AssertGrowingObjectDeniedAsync(AclAction action, System.Action<IGrowingObjectCustomer> sendRequest)
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);

            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;

            var denyRule = new AclRule
            {
                Username = username,
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = action,
                Effect = AclEffect.Deny
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter>
                {
                    new AllowEndpointAuthorizationAdapter()
                };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                aclRuleStore.Save(denyRule);

                await RequestSessionAndAssert(10, Protocols.ChannelStreaming, Protocols.Discovery, Protocols.Store, Protocols.GrowingObject, Protocols.StoreNotification);

                var handler = _client.Handler<IGrowingObjectCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(
                    x => handler.OnProtocolException += x);

                sendRequest(handler);

                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, action);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public async Task Log131_Etp12GetPart_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObject12DeniedAsync(AclAction.Read,
                handler => handler.GetPart(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_Etp12GetPartsByRange_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObject12DeniedAsync(AclAction.Read,
                handler => handler.GetPartsByRange(Log.GetUri().ToString(), 0L, 10L, string.Empty, string.Empty));
        }

        [TestMethod]
        public async Task Log131_Etp12PutPart_Denies_Explicit_Update_Deny()
        {
            await AssertGrowingObject12DeniedAsync(AclAction.Update,
                handler => handler.PutPart(Log.GetUri().ToString(), "part", "text/xml", new byte[0]));
        }

        [TestMethod]
        public async Task Log131_Etp12DeletePart_Denies_Explicit_Delete_Deny()
        {
            await AssertGrowingObject12DeniedAsync(AclAction.Delete,
                handler => handler.DeletePart(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_Etp12DeletePartsByRange_Denies_Explicit_Delete_Deny()
        {
            await AssertGrowingObject12DeniedAsync(AclAction.Delete,
                handler => handler.DeletePartsByRange(Log.GetUri().ToString(), 0L, 10L, string.Empty, string.Empty));
        }

        private async Task AssertGrowingObject12DeniedAsync(AclAction action, System.Action<GrowingObject12Customer> sendRequest)
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);

            var aclRuleStore = DevKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);

            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var username = TestSettings.Username;

            var denyRule = new AclRule
            {
                Username = username,
                ResourceId = Log.Uid,
                ResourceType = AclResourceType.DataObject,
                Action = action,
                Effect = AclEffect.Deny
            };

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter>
                {
                    new AllowEndpointAuthorizationAdapter()
                };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                aclRuleStore.Save(denyRule);

                await RequestEtp12SessionAndAssert(_client);

                var handler = _client.Handler<GrowingObject12Customer>();
                var onProtocolException = HandleAsync<IProtocolException>(
                    x => handler.OnProtocolException += x);

                sendRequest(handler);

                var exception = await onProtocolException;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                aclRuleStore.Delete(username, Log.Uid, AclResourceType.DataObject, action);
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        private sealed class AllowEndpointAuthorizationAdapter : IUserAuthorizationAdapter
        {
            public bool IsAuthorized(string username, RequestContext request, WitsmlEndpointTypes endpointType)
            {
                return true;
            }
        }

        [TestMethod, Description("Tests that 131 Log Data can be added when Compatibility Setting LogAllowPutObjectWithData is True")]
        public async Task Log131_PutObject_Can_Add_Log_Data_With_LogAllowPutObjectWithData_True()
        {
            const int numRows = 10;
            const bool allowPutData = true;

            await Log131_PutObject_Can_Add_Log_Data_With_LogAllowPutObjectWithData(numRows, allowPutData);
        }

        [TestMethod, Description("Tests that 131 Log Data cannot be added when Compatibility Setting LogAllowPutObjectWithData is False")]
        public async Task Log131_PutObject_Can_Add_Log_Data_With_LogAllowPutObjectWithData_False()
        {
            const int numRows = 10;
            const bool allowPutData = false;

            await Log131_PutObject_Can_Add_Log_Data_With_LogAllowPutObjectWithData(numRows, allowPutData);
        }

        private async Task Log131_PutObject_Can_Add_Log_Data_With_LogAllowPutObjectWithData(int numRows, bool allowPutData)
        {
            AddParents();

            // Allow for Log data to be saved during a Put
            CompatibilitySettings.LogAllowPutObjectWithData = allowPutData;

            await RequestSessionAndAssert(10, Protocols.ChannelStreaming, Protocols.Discovery, Protocols.Store, Protocols.GrowingObject, Protocols.StoreNotification);

            var handler = _client.Handler<IStoreCustomer>();
            var uri = Log.GetUri();
            DevKit.InitHeader(Log, LogIndexType.measureddepth);
            DevKit.InitDataMany(Log, DevKit.Mnemonics(Log), DevKit.Units(Log), numRows);

            var dataObject = CreateDataObject<LogList, Log>(uri, Log);

            // Put Object
            await PutAndAssert(handler, dataObject);

            // Get Object
            var result = DevKit.GetAndAssert<LogList, Log>(DevKit.CreateLog(Log));

            // Verify that the Log was saved
            Assert.IsNotNull(result);

            // Verify the number of Log Data saved.
            var rowsExpected = allowPutData ? numRows : 0;
            Assert.IsNotNull(result.LogData);
            Assert.AreEqual(rowsExpected, result.LogData.Count);
        }
    }
}

namespace PDS.WITSMLstudio.Store.Data.Logs
{
/// <summary>
/// Test-only ETP 1.1 customer handler that sends all required fields for
/// GrowingObjectDeleteRange. The upstream DevKit handler currently omits
/// Uom and DepthDatum when building that message.
/// </summary>
internal sealed class AclGrowingObjectCustomerHandler : Energistics.Etp.v11.Protocol.GrowingObject.GrowingObjectCustomerHandler
{
    public long GrowingObjectDeleteRangeWithAclFields(string uri, object startIndex, object endIndex, string uom, string depthDatum)
    {
        var header = CreateMessageHeader(
            Energistics.Etp.v11.Protocols.GrowingObject,
            Energistics.Etp.v11.MessageTypes.GrowingObject.GrowingObjectDeleteRange);

        var message = new Energistics.Etp.v11.Protocol.GrowingObject.GrowingObjectDeleteRange
        {
            Uri = uri,
            StartIndex = new Energistics.Etp.v11.Datatypes.Object.GrowingObjectIndex { Item = startIndex },
            EndIndex = new Energistics.Etp.v11.Datatypes.Object.GrowingObjectIndex { Item = endIndex },
            Uom = uom ?? string.Empty,
            DepthDatum = depthDatum ?? string.Empty
        };

        return Session.SendMessage(header, message);
    }
}
}
