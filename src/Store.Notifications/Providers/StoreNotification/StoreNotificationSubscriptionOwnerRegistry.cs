//----------------------------------------------------------------------- 
// PDS WITSMLstudio Store
//
// Shared owner registry for Store Notification subscriptions across ETP sessions.
// The per-session protocol handlers retain their own delivery state; this registry
// is used only to enforce ownership when a cancellation arrives on another session.
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace PDS.WITSMLstudio.Store.Providers.StoreNotification
{
    internal static class StoreNotificationSubscriptionOwnerRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, string> Owners =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool TryRegister(string key, string owner)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Subscription key is required.", nameof(key));
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Subscription owner is required.", nameof(owner));

            lock (SyncRoot)
            {
                string existingOwner;
                if (Owners.TryGetValue(key, out existingOwner))
                    return string.Equals(existingOwner, owner, StringComparison.OrdinalIgnoreCase);

                Owners.Add(key, owner);
                return true;
            }
        }

        public static bool TryGetOwner(string key, out string owner)
        {
            lock (SyncRoot)
            {
                return Owners.TryGetValue(key, out owner);
            }
        }

        public static void Remove(string key, string owner)
        {
            lock (SyncRoot)
            {
                string currentOwner;
                if (Owners.TryGetValue(key, out currentOwner) &&
                    string.Equals(currentOwner, owner, StringComparison.OrdinalIgnoreCase))
                {
                    Owners.Remove(key);
                }
            }
        }
    }
}
