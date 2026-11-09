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

using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using Energistics.DataAccess;
using Energistics.DataAccess.WITSML141;
using Energistics.DataAccess.WITSML141.ReferenceData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PDS.WITSMLstudio.Store.Configuration;
using PDS.WITSMLstudio.Store.Data.Security;

namespace PDS.WITSMLstudio.Store
{
    [TestClass]
    public class WitsmlStore141Tests
    {
        private DevKit141Aspect _devKit;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestSetUp()
        {
            _devKit = new DevKit141Aspect(TestContext);

            _devKit.Store.CapServerProviders = _devKit.Store.CapServerProviders
                .Where(x => x.DataSchemaVersion == OptionsIn.DataVersion.Version141.Value)
                .ToArray();
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            WitsmlSettings.TruncateXmlOutDebugSize = DevKitAspect.DefaultXmlOutDebugSize;
        }

        [TestMethod]
        public void WitsmlStore_GetVersion_Can_Get_Version()
        {
            var request = new WMLS_GetVersionRequest();
            var response = _devKit.Store.WMLS_GetVersion(request);

            Assert.IsNotNull(response);
            if (!string.IsNullOrEmpty(response.Result))
            {
                var versions = response.Result.Split(',');
                Assert.IsNotNull(versions);
                Assert.IsTrue(versions.Length > 0);

                foreach (var version in versions)
                    Assert.IsFalse(string.IsNullOrEmpty(version));
            }
        }

        [TestMethod]
        public void WitsmlStore_GetVersion_Version_Ordered_Oldest_First()
        {
            var request = new WMLS_GetVersionRequest();
            var response = _devKit.Store.WMLS_GetVersion(request);

            Assert.IsNotNull(response);
            var ordered = true;
            if (!string.IsNullOrEmpty(response.Result))
            {
                var versions = response.Result.Split(',');
                Assert.IsNotNull(versions);
                Assert.IsTrue(versions.Length > 0);

                var version = versions[0];
                Assert.IsFalse(string.IsNullOrEmpty(version));

                for (var i = 1; i < versions.Length; i++)
                {
                    if (String.CompareOrdinal(version, versions[i]) >= 0)
                    {
                        ordered = false;
                        break;
                    }
                    version = versions[i];
                }
            }

            Assert.IsTrue(ordered);
        }

        [TestMethod]
        public void WitsmlStore_GetCap_Can_Get_Cap_Server()
        {
            var request = new WMLS_GetCapRequest { OptionsIn = "dataVersion=1.4.1.1" };
            var response = _devKit.Store.WMLS_GetCap(request);

            Assert.IsNotNull(response);
            Assert.IsFalse(string.IsNullOrEmpty(response.CapabilitiesOut));
        }

        [TestMethod]
        public void Witsml_Store_GetFromStore_Error_401_Missing_Plural_Root_Element()
        {
            var list = new LogList();
            var xmlIn = EnergisticsConverter.ObjectToXml(list).Replace("logs", "log");
            var response = _devKit.GetFromStore(ObjectTypes.Log, xmlIn, null, null);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.MissingPluralRootElement, response.Result);
        }

        [Ignore, Description("Not Implemented")]
        [TestMethod]
        public void WitsmlStore_AddToStore_Error_404_Invalid_Schema_Version()
        {
            var client = new CapClient { ApiVers = "1.4.1.1", SchemaVersion = "1.4.1.1,1.3.1.1" };
            var clients = new CapClients { Version = "1.4.1.1", CapClient = client };
            var capabilitiesIn = EnergisticsConverter.ObjectToXml(clients);
            var well = new Well { Name = "Well-to-add-invalid-schema-version" };
            var response = _devKit.Add<WellList, Well>(well, capClient: capabilitiesIn);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.InvalidClientSchemaVersion, response.Result);
        }

        [TestMethod]
        public void WitsmlStore_GetCap_Error_423_Unsupported_Data_Version()
        {
            var request = new WMLS_GetCapRequest { OptionsIn = "dataVersion=1.6.1.1" };
            var response = _devKit.Store.WMLS_GetCap(request);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.DataVersionNotSupported, response.Result);
        }

        [TestMethod]
        public void WitsmlStore_GetCap_Error_424_Missing_Data_Version()
        {
            var request = new WMLS_GetCapRequest();
            var response = _devKit.Store.WMLS_GetCap(request);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.MissingDataVersion, response.Result);
        }

        [Ignore, Description("Not Implemented")]
        [TestMethod]
        public void WitsmlStore_AddToStore_Error_465_Api_Version_Not_Match()
        {
            var client = new CapClient { ApiVers = "1.3.1.1", SchemaVersion = "1.3.1.1" };
            var clients = new CapClients { Version = "1.4.1.1", CapClient = client };
            var capabilitiesIn = EnergisticsConverter.ObjectToXml(clients);
            var well = new Well { Name = _devKit.Name("Well-to-add-apiVers-not-match"), TimeZone = _devKit.TimeZone };
            var response = _devKit.Add<WellList, Well>(well, capClient: capabilitiesIn);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.ApiVersionNotMatch, response.Result);
        }

        [Ignore, Description("Not Implemented")]
        [TestMethod]
        public void WitsmlStore_AddToStore_Error_467_Unsupported_Data_Schema_Version()
        {
            var client = new CapClient { ApiVers = "1.4.1.1"};
            var clients = new CapClients { Version = "1.4.x.y", CapClient = client };
            var capabilitiesIn = EnergisticsConverter.ObjectToXml(clients);
            var well = new Well { Name = _devKit.Name("Well-to-add-unsupported-schema-version"), TimeZone = _devKit.TimeZone };
            var response = _devKit.Add<WellList, Well>(well, capClient: capabilitiesIn);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.ApiVersionNotSupported, response.Result);
        }

        [Ignore, Description("Not Implemented")]
        [TestMethod]
        public void WitsmlStore_AddToStore_Error_473_Schema_Version_Not_Match()
        {
            var client = new CapClient { ApiVers = "1.4.1.1", SchemaVersion = "1.3.1.1" };
            var clients = new CapClients { Version = "1.4.1.1", CapClient = client };
            var capabilitiesIn = EnergisticsConverter.ObjectToXml(clients);
            var well = new Well { Name = _devKit.Name("Well-to-add-schema-version-not-match"), TimeZone = _devKit.TimeZone };
            var response = _devKit.Add<WellList, Well>(well, capClient: capabilitiesIn);

            Assert.IsNotNull(response);
            Assert.AreEqual((short)ErrorCodes.SchemaVersionNotMatch, response.Result);
        }

        [TestMethod]
        public void WitsmlStore_GetBaseMsg_Can_Return_Message()
        {
            var request = new WMLS_GetBaseMsgRequest {ReturnValueIn = (short) ErrorCodes.InputTemplateNonConforming };
            var response = _devKit.Store.WMLS_GetBaseMsg(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Result);
            Assert.AreEqual("The input template must conform to the appropriate derived schema.", response.Result);
        }

        [TestMethod]
        public void WitsmlStore_GetBaseMsg_Error_422_ReturnValueIn_Is_UnSet()
        {
            var request = new WMLS_GetBaseMsgRequest();
            var response = _devKit.Store.WMLS_GetBaseMsg(request);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Result);
            Assert.IsTrue(response.Result.Contains("-422"));
        }

        [TestMethod]
        public void WitsmlStore_GetBaseMsg_Can_Return_Null_On_Invalid_ReturnValueIn()
        {
            var request = new WMLS_GetBaseMsgRequest { ReturnValueIn = 12345 };
            var response = _devKit.Store.WMLS_GetBaseMsg(request);

            Assert.IsNotNull(response);
            Assert.IsNull(response.Result);
        }

        [TestMethod]
        public void WitsmlStore141_Soap_CRUD_Enforces_Object_ACL()
        {
            var aclRuleStore = _devKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = _devKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);

            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var originalPrincipal = Thread.CurrentPrincipal;
            var username = "acl-soap-" + Guid.NewGuid().ToString("N");
            var well = new Well
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL SOAP Well"),
                TimeZone = _devKit.TimeZone
            };
            var deniedCreateWell = new Well
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL SOAP Denied Create"),
                TimeZone = _devKit.TimeZone
            };
            var wellCreated = false;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = false;
                var addResponse = _devKit.Add<WellList, Well>(well);
                Assert.AreEqual((short)ErrorCodes.Success, addResponse.Result);
                wellCreated = true;

                Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity(username), new string[0]);
                WitsmlSettings.AclAdministratorUsers = string.Empty;
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter>
                {
                    new AllowSoapAuthorizationAdapter()
                };
                WitsmlSettings.IsUserAuthorizationEnabled = true;

                aclRuleStore.Save(new AclRule
                {
                    Username = username,
                    ResourceId = deniedCreateWell.Uid,
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Create,
                    Effect = AclEffect.Deny
                });

                var deniedCreateResponse = _devKit.Add<WellList, Well>(deniedCreateWell);
                Assert.AreEqual((short)ErrorCodes.InsufficientOperationRights, deniedCreateResponse.Result);

                foreach (var action in new[] { AclAction.Read, AclAction.Update, AclAction.Delete })
                {
                    aclRuleStore.Save(new AclRule
                    {
                        Username = username,
                        ResourceId = well.Uid,
                        ResourceType = AclResourceType.Well,
                        Action = action,
                        Effect = AclEffect.Deny
                    });
                }

                var readResult = _devKit.QueryAndAssert<WellList, Well>(
                    new Well { Uid = well.Uid },
                    isNotNull: false);
                Assert.IsNull(readResult);

                var updateResponse = _devKit.Update<WellList, Well>(new Well
                {
                    Uid = well.Uid,
                    Name = well.Name,
                    TimeZone = well.TimeZone
                });
                Assert.AreEqual((short)ErrorCodes.InsufficientOperationRights, updateResponse.Result);

                var deleteResponse = _devKit.Delete<WellList, Well>(new Well { Uid = well.Uid });
                Assert.AreEqual((short)ErrorCodes.InsufficientOperationRights, deleteResponse.Result);
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = false;

                foreach (var action in new[] { AclAction.Create, AclAction.Read, AclAction.Update, AclAction.Delete })
                {
                    aclRuleStore.Delete(username, well.Uid, AclResourceType.Well, action);
                    aclRuleStore.Delete(username, deniedCreateWell.Uid, AclResourceType.Well, action);
                }

                if (wellCreated)
                    _devKit.Delete<WellList, Well>(new Well { Uid = well.Uid });
                _devKit.Delete<WellList, Well>(new Well { Uid = deniedCreateWell.Uid });

                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
                Thread.CurrentPrincipal = originalPrincipal;
            }
        }


        [TestMethod]
        public void WitsmlStore141_Soap_Enforces_Parent_ACL_Hierarchy()
        {
            var aclRuleStore = _devKit.Container.Resolve<IAclRuleStore>();
            var authorizationProvider = _devKit.Container.Resolve<IUserAuthorizationProvider>() as UserAuthorizationProvider;
            Assert.IsNotNull(authorizationProvider);

            var originalAuthorizationEnabled = WitsmlSettings.IsUserAuthorizationEnabled;
            var originalAdministrators = WitsmlSettings.AclAdministratorUsers;
            var originalAdapters = authorizationProvider.UserAuthorizationAdapters;
            var originalPrincipal = Thread.CurrentPrincipal;
            var username = "acl-soap-hierarchy-" + Guid.NewGuid().ToString("N");

            var well = new Well
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL Hierarchy Well"),
                TimeZone = _devKit.TimeZone
            };
            var wellbore = new Wellbore
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL Hierarchy Wellbore"),
                UidWell = well.Uid,
                NameWell = well.Name
            };
            var log = new Log
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL Hierarchy Log"),
                UidWell = well.Uid,
                NameWell = well.Name,
                UidWellbore = wellbore.Uid,
                NameWellbore = wellbore.Name
            };
            var deniedWellbore = new Wellbore
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL Denied Wellbore"),
                UidWell = well.Uid,
                NameWell = well.Name
            };
            var deniedLog = new Log
            {
                Uid = _devKit.Uid(),
                Name = _devKit.Name("ACL Denied Log"),
                UidWell = well.Uid,
                NameWell = well.Name,
                UidWellbore = wellbore.Uid,
                NameWellbore = wellbore.Name
            };

            var wellCreated = false;
            var wellboreCreated = false;
            var logCreated = false;

            try
            {
                WitsmlSettings.IsUserAuthorizationEnabled = false;

                var wellResponse = _devKit.Add<WellList, Well>(well);
                Assert.AreEqual((short)ErrorCodes.Success, wellResponse.Result);
                wellCreated = true;

                var wellboreResponse = _devKit.Add<WellboreList, Wellbore>(wellbore);
                Assert.AreEqual((short)ErrorCodes.Success, wellboreResponse.Result);
                wellboreCreated = true;

                _devKit.InitHeader(log, LogIndexType.measureddepth);
                var logResponse = _devKit.Add<LogList, Log>(log);
                Assert.AreEqual((short)ErrorCodes.Success, logResponse.Result);
                logCreated = true;

                Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity(username), new string[0]);
                WitsmlSettings.AclAdministratorUsers = string.Empty;
                authorizationProvider.UserAuthorizationAdapters = new List<IUserAuthorizationAdapter>
                {
                    new AllowSoapAuthorizationAdapter()
                };
                WitsmlSettings.IsUserAuthorizationEnabled = true;

                // Create is checked against the immediate parent, even without inheritance.
                aclRuleStore.Save(new AclRule
                {
                    Username = username,
                    ResourceId = well.Uid,
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Create,
                    Effect = AclEffect.Deny,
                    Inherit = false
                });
                var deniedWellboreResponse = _devKit.Add<WellboreList, Wellbore>(deniedWellbore);
                Assert.AreEqual((short)ErrorCodes.InsufficientOperationRights, deniedWellboreResponse.Result);
                aclRuleStore.Delete(username, well.Uid, AclResourceType.Well, AclAction.Create);

                aclRuleStore.Save(new AclRule
                {
                    Username = username,
                    ResourceId = wellbore.Uid,
                    ResourceType = AclResourceType.Wellbore,
                    Action = AclAction.Create,
                    Effect = AclEffect.Deny,
                    Inherit = false
                });
                var deniedLogResponse = _devKit.Add<LogList, Log>(deniedLog);
                Assert.AreEqual((short)ErrorCodes.InsufficientOperationRights, deniedLogResponse.Result);
                aclRuleStore.Delete(username, wellbore.Uid, AclResourceType.Wellbore, AclAction.Create);

                // A Well-level Read Deny must flow through Wellbore to the Log data object.
                aclRuleStore.Save(new AclRule
                {
                    Username = username,
                    ResourceId = well.Uid,
                    ResourceType = AclResourceType.Well,
                    Action = AclAction.Read,
                    Effect = AclEffect.Deny,
                    Inherit = true
                });
                var wellDeniedRead = _devKit.QueryAndAssert<LogList, Log>(
                    new Log { Uid = log.Uid, UidWell = well.Uid, UidWellbore = wellbore.Uid },
                    isNotNull: false);
                Assert.IsNull(wellDeniedRead);
                aclRuleStore.Delete(username, well.Uid, AclResourceType.Well, AclAction.Read);

                // A Wellbore-level Read Deny must flow through to its Log data object.
                aclRuleStore.Save(new AclRule
                {
                    Username = username,
                    ResourceId = wellbore.Uid,
                    ResourceType = AclResourceType.Wellbore,
                    Action = AclAction.Read,
                    Effect = AclEffect.Deny,
                    Inherit = true
                });
                var wellboreDeniedRead = _devKit.QueryAndAssert<LogList, Log>(
                    new Log { Uid = log.Uid, UidWell = well.Uid, UidWellbore = wellbore.Uid },
                    isNotNull: false);
                Assert.IsNull(wellboreDeniedRead);
            }
            finally
            {
                WitsmlSettings.IsUserAuthorizationEnabled = false;

                foreach (var action in new[] { AclAction.Create, AclAction.Read })
                {
                    aclRuleStore.Delete(username, well.Uid, AclResourceType.Well, action);
                    aclRuleStore.Delete(username, wellbore.Uid, AclResourceType.Wellbore, action);
                }

                if (logCreated)
                    _devKit.Delete<LogList, Log>(new Log { Uid = log.Uid, UidWell = well.Uid, UidWellbore = wellbore.Uid });
                if (wellboreCreated)
                    _devKit.Delete<WellboreList, Wellbore>(new Wellbore { Uid = wellbore.Uid, UidWell = well.Uid });
                if (wellCreated)
                    _devKit.Delete<WellList, Well>(new Well { Uid = well.Uid });

                WitsmlSettings.IsUserAuthorizationEnabled = originalAuthorizationEnabled;
                WitsmlSettings.AclAdministratorUsers = originalAdministrators;
                authorizationProvider.UserAuthorizationAdapters = originalAdapters;
                Thread.CurrentPrincipal = originalPrincipal;
            }
        }

        private sealed class AllowSoapAuthorizationAdapter : IUserAuthorizationAdapter
        {
            public bool IsAuthorized(string username, RequestContext request, WitsmlEndpointTypes endpointType)
            {
                return true;
            }
        }
    }
}
