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
using Energistics.Etp;
using Energistics.Etp.Common;
using Energistics.Etp.Common.Protocol.Core;
using Energistics.Etp.v11.Protocol.Core;
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
        [TestMethod]
        public async Task Log131_GrowingObjectGet_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Read,
                handler => handler.GrowingObjectGet(Log.GetUri().ToString(), "missing-part"));
        }

        [TestMethod]
        public async Task Log131_GrowingObjectGetRange_Denies_Explicit_Read_Deny()
        {
            await AssertGrowingObjectDeniedAsync(AclAction.Read,
                handler => handler.GrowingObjectGetRange(Log.GetUri().ToString(), 0, 10, null, null));
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
                handler => handler.GrowingObjectDeleteRange(Log.GetUri().ToString(), 0, 10, null, null));
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
                await RequestSessionAndAssert();
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
                await RequestSessionAndAssert();
                aclRuleStore.Save(denyRule);

                var handler = _client.Handler<Energistics.Etp.v12.Protocol.StoreNotification.IStoreNotificationCustomer>();
                var onProtocolException = HandleAsync<IProtocolException>(x => handler.OnProtocolException += x);
                handler.SubscribeNotification(new Energistics.Etp.v12.Datatypes.Object.SubscriptionInfo
                {
                    Context = new Energistics.Etp.v12.Datatypes.Object.ContextInfo
                    {
                        Uri = Log.GetUri().ToString(),
                        Depth = 0
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
        public async Task Log131_Etp11StoreNotification_Prevents_Other_User_From_Canceling_Subscription()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var requestUuid = System.Guid.NewGuid().ToString();
            IEtpClient otherClient = null;

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestSessionAndAssert();

                var ownerHandler = _client.Handler<Energistics.Etp.v11.Protocol.StoreNotification.IStoreNotificationCustomer>();
                ownerHandler.NotificationRequest(new Energistics.Etp.v11.Datatypes.Object.NotificationRequestRecord
                {
                    IncludeObjectData = true,
                    ObjectTypes = new List<string>(),
                    StartTime = (long)((System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds * 1000L),
                    Uri = Log.GetUri().ToString(),
                    Uuid = requestUuid
                });

                otherClient = InitClient(CreateClient(_etpServerUrl, "acl-other-user", TestSettings.Password));
                var otherSession = HandleAsync<OpenSession>(x => otherClient.Handler<ICoreClient>().OnOpenSession += x);
                Assert.IsTrue(await otherClient.OpenAsyncWithTimeout());
                await otherSession;

                var otherHandler = otherClient.Handler<Energistics.Etp.v11.Protocol.StoreNotification.IStoreNotificationCustomer>();
                var denied = HandleAsync<IProtocolException>(x => otherHandler.OnProtocolException += x);
                otherHandler.CancelNotification(requestUuid);
                var exception = await denied;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                otherClient?.Dispose();
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
            }
        }

        [TestMethod]
        public async Task Log131_Etp12StoreNotification_Prevents_Other_User_From_Unsubscribing()
        {
            AddParents();
            DevKit.AddAndAssert<LogList, Log>(Log);
            var authorizationProvider = DevKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);
            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var requestUuid = System.Guid.NewGuid();
            IEtpClient otherClient = null;

            try
            {
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter> { new AllowEndpointAuthorizationAdapter() };
                WitsmlSettings.IsUserAuthorizationEnabled = true;
                await RequestSessionAndAssert();

                var ownerHandler = _client.Handler<Energistics.Etp.v12.Protocol.StoreNotification.IStoreNotificationCustomer>();
                ownerHandler.SubscribeNotification(new Energistics.Etp.v12.Datatypes.Object.SubscriptionInfo
                {
                    Context = new Energistics.Etp.v12.Datatypes.Object.ContextInfo
                    {
                        Uri = Log.GetUri().ToString(),
                        Depth = 0
                    },
                    Scope = Energistics.Etp.v12.Datatypes.Object.ContextScopeKind.self,
                    IncludeObjectData = true,
                    RequestUuid = requestUuid.ToUuid()
                });

                otherClient = InitClient(CreateClient(_etpServerUrl, "acl-other-user", TestSettings.Password));
                var otherSession = HandleAsync<OpenSession>(x => otherClient.Handler<ICoreClient>().OnOpenSession += x);
                Assert.IsTrue(await otherClient.OpenAsyncWithTimeout());
                await otherSession;

                var otherHandler = otherClient.Handler<Energistics.Etp.v12.Protocol.StoreNotification.IStoreNotificationCustomer>();
                var denied = HandleAsync<IProtocolException>(x => otherHandler.OnProtocolException += x);
                otherHandler.UnsubscribeNotification(requestUuid);
                var exception = await denied;
                Assert.IsNotNull(exception?.Message);
                Assert.AreEqual((int)ErrorCodes.InsufficientOperationRights, exception.Message.ErrorCode);
            }
            finally
            {
                otherClient?.Dispose();
                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
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
                await RequestSessionAndAssert();
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
                await RequestSessionAndAssert();
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

                await RequestSessionAndAssert();

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
                handler => handler.GetPartsByRange(Log.GetUri().ToString(), 0, 10, null, null));
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
                handler => handler.DeletePartsByRange(Log.GetUri().ToString(), 0, 10, null, null));
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

                await RequestSessionAndAssert();

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

            await RequestSessionAndAssert();

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
