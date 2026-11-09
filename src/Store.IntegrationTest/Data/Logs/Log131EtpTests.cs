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
using Energistics.Etp.v11.Protocol.GrowingObject;
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
