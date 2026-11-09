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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Energistics.DataAccess;
using Witsml200 = Energistics.DataAccess.WITSML200;
using Energistics.Etp.Common.Datatypes;
using Energistics.Etp.v12.Datatypes;
using Energistics.Etp.v12.Datatypes.Object;
using Energistics.Etp.v12.Protocol.StoreNotification;
using Newtonsoft.Json.Linq;
using PDS.WITSMLstudio.Framework;
using PDS.WITSMLstudio.Store;
using PDS.WITSMLstudio.Store.Data;
using PDS.WITSMLstudio.Store.Data.Security;

namespace PDS.WITSMLstudio.Store.Providers.StoreNotification
{
    /// <summary>
    /// Provides a common framework for all Store Notification Store provider implementations.
    /// </summary>
    /// <seealso cref="StoreNotificationStoreHandler" />
    public abstract class StoreNotification12StoreProviderBase : StoreNotificationStoreHandler
    {
        private readonly Dictionary<Uuid, IMessageHeader> _headers;
        private readonly List<SubscribeNotification> _requests;
        private readonly Dictionary<Uuid, string> _subscribers;

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreNotification12StoreProviderBase"/> class.
        /// </summary>
        protected StoreNotification12StoreProviderBase()
        {
            _headers = new Dictionary<Uuid, IMessageHeader>();
            _requests = new List<SubscribeNotification>();
            _subscribers = new Dictionary<Uuid, string>();
        }

        /// <summary>Authorization service used to filter asynchronous notifications.</summary>
        [Import]
        public IUserAuthorizationProvider AuthorizationProvider { get; set; }

        /// <summary>MEF container used to resolve the canonical WITSML data provider.</summary>
        [Import]
        public IContainer Container { get; set; }

        /// <summary>
        /// Handles the subscribe notification request.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="request">The request.</param>
        protected override void HandleSubscribeNotification(IMessageHeader header, SubscribeNotification request)
        {
            CheckSubscriptionReadAccess(request.Request.Context.Uri);
            base.HandleSubscribeNotification(header, request);
            EnsureConnection();

            if (_requests.Any(x => x.Request.RequestUuid.Equals(request.Request.RequestUuid)))
            {
                // TODO: Should this be an error?
            }
            else
            {
                _headers[request.Request.RequestUuid] = header;
                _subscribers[request.Request.RequestUuid] = WitsmlOperationContext.Current.User;
                _requests.Add(request);
            }
        }

        /// <summary>
        /// Handles the unsubscribe notification request.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="request">The request.</param>
        protected override void HandleUnsubscribeNotification(IMessageHeader header, UnsubscribeNotification request)
        {
            var message = _requests.FirstOrDefault(x => x.Request.RequestUuid.Equals(request.RequestUuid));
            if (message != null) EnsureSubscriptionOwner(_subscribers[message.Request.RequestUuid]);

            base.HandleUnsubscribeNotification(header, request);

            if (message == null)
            {
                // TODO: Should this be an error?
            }
            else
            {
                _requests.Remove(message);
                _headers.Remove(message.Request.RequestUuid);
                _subscribers.Remove(message.Request.RequestUuid);
            }
        }

        private void CheckSubscriptionReadAccess(string uri)
        {
            var etpUri = new EtpUri(uri);
            var provider = Container.Resolve<IEtpDataProvider>(
                new ObjectName(etpUri.ObjectType, etpUri.Family, etpUri.Version));
            provider.CheckObjectAccess(WitsmlOperationContext.Current.User, AclAction.Read, etpUri);
        }

        private void EnsureSubscriptionOwner(string owner)
        {
            if (!string.Equals(owner, WitsmlOperationContext.Current.User, StringComparison.OrdinalIgnoreCase))
                throw new WitsmlException(ErrorCodes.InsufficientOperationRights);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disconnect();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Ensures the connection to the message broker.
        /// </summary>
        protected virtual void EnsureConnection()
        {
        }

        /// <summary>
        /// Disconnects from the message broker.
        /// </summary>
        protected virtual void Disconnect()
        {
        }

        protected virtual void OnNotifyInsert(string uri, object dataObject, DateTime dateTime)
        {
            OnNotify(ObjectChanged, uri, dataObject, dateTime, ObjectChangeKind.insert);
        }

        protected virtual void OnNotifyUpdate(string uri, object dataObject, DateTime dateTime)
        {
            OnNotify(ObjectChanged, uri, dataObject, dateTime, ObjectChangeKind.update);
        }

        protected virtual void OnNotifyDelete(string uri, object dataObject, DateTime dateTime)
        {
            var request = _requests.FirstOrDefault(x => x.Request.Context.Uri.EqualsIgnoreCase(uri));
            if (request == null) return;

            IMessageHeader header;
            string username;
            if (!_headers.TryGetValue(request.Request.RequestUuid, out header)
                || !_subscribers.TryGetValue(request.Request.RequestUuid, out username)) return;

            try
            {
                var etpUri = new EtpUri(uri);
                var provider = Container.Resolve<IEtpDataProvider>(
                    new ObjectName(etpUri.ObjectType, etpUri.Family, etpUri.Version));
                provider.CheckObjectAccess(username, AclAction.Read, etpUri, ParseNotificationObject(etpUri, dataObject));
            }
            catch (WitsmlException)
            {
                return;
            }

            ObjectDeleted(header, uri, dateTime.ToUnixTimeMicroseconds());
        }

        protected virtual void OnNotify(Func<IMessageHeader, ObjectChange, long> action, string uri, object dataObject, DateTime dateTime, ObjectChangeKind changeKind)
        {
            var request = _requests.FirstOrDefault(x => x.Request.Context.Uri.EqualsIgnoreCase(uri));
            if (request == null) return;

            IMessageHeader header;
            string username;
            if (!_headers.TryGetValue(request.Request.RequestUuid, out header)
                || !_subscribers.TryGetValue(request.Request.RequestUuid, out username)) return;

            var etpUri = new EtpUri(uri);

            try
            {
                var provider = Container.Resolve<IEtpDataProvider>(
                    new ObjectName(etpUri.ObjectType, etpUri.Family, etpUri.Version));
                provider.CheckObjectAccess(username, AclAction.Read, etpUri, ParseNotificationObject(etpUri, dataObject));
            }
            catch (WitsmlException)
            {
                return;
            }

            action(header, new ObjectChange
            {
                ChangeKind = changeKind,
                ChangeTime = dateTime.ToUnixTimeMicroseconds(),
                DataObject = GetDataObject(etpUri.ObjectType, etpUri.Family, etpUri.Version, dataObject, request.Request.IncludeObjectData)
            });
        }

        private static object ParseNotificationObject(EtpUri uri, object dataObject)
        {
            var jObject = dataObject as JObject;
            if (jObject == null && !(dataObject is string))
                return dataObject;

            var type = ObjectTypes.GetObjectGroupType(uri.ObjectType, uri.Family, uri.Version) ??
                       ObjectTypes.GetObjectType(uri.ObjectType, uri.Family, uri.Version);

            var parsed = jObject != null
                ? jObject.ToObject(type)
                : WitsmlParser.Parse(type, WitsmlParser.Parse(dataObject.ToString()).Root);

            // WITSML 1.x payloads may be collection wrappers; ACL references
            // belong to the contained data object rather than the wrapper.
            var collection = parsed as IEnergisticsCollection;
            return collection == null
                ? parsed
                : collection.Items.OfType<IDataObject>().FirstOrDefault();
        }

        protected virtual DataObject GetDataObject(string objectType, string family, string version, object dataObject, bool includeObjectData)
        {
            var jObject = dataObject as JObject;

            if (jObject != null || dataObject is string)
            {
                var type = ObjectTypes.GetObjectGroupType(objectType, family, version) ??
                           ObjectTypes.GetObjectType(objectType, family, version);

                dataObject = jObject?.ToObject(type) ??
                    WitsmlParser.Parse(type, WitsmlParser.Parse(dataObject.ToString()).Root);
            }

            var collection = dataObject as IEnergisticsCollection;
            var iDataObject = collection?.Items?.OfType<IDataObject>().FirstOrDefault();
            var cDataObject = iDataObject as ICommonDataObject;
            var aDataObject = dataObject as Witsml200.AbstractObject;

            var uri = iDataObject?.GetUri() ?? aDataObject?.GetUri() ?? new EtpUri();
            var name = iDataObject?.Name ?? aDataObject?.Citation?.Title;
            var lastChanged = cDataObject?.CommonData?.DateTimeLastChange?.ToUnixTimeMicroseconds() ??
                              aDataObject?.Citation?.LastUpdate?.ToUnixTimeMicroseconds();

            var etpDataObject = new DataObject();

            // Do not return DataObject.Data if not requested in original subscription
            Session.Adapter.SetDataObject(
                etpDataObject,
                includeObjectData ? dataObject : null,
                uri,
                name,
                -1,
                lastChanged.GetValueOrDefault());

            return etpDataObject;
        }
    }
}
