# Object-level ACL

The object ACL is evaluated by the Store on the server for SOAP and ETP requests. It is not a client-side filter.

## Enablement

Set the existing application setting `IsUserAuthorizationEnabled` to `true`. It is `false` by default for backward compatibility. When enabled, the existing endpoint authorization adapters continue to run and object ACL checks are added.

Configure trusted administrator usernames in `src/Store/Web.config`:

```xml
<add key="AclAdministratorUsers" value="store-admin;security-admin" />
```

The value is a comma- or semicolon-separated list. It is read from server configuration; a client claim cannot grant administrator bypass. Administrator bypass applies to object ACL only and does not bypass the existing SOAP/ETP endpoint authorization checks.

## MongoDB rules

Rules are stored in the `AclRules` collection in the configured WITSML database. Each rule contains:

| Field | Meaning |
| --- | --- |
| `Username` | Authenticated username, or `*` for all users |
| `ResourceId` | WITSML object identifier (UID/UUID from the canonical object URI) |
| `ResourceType` | `0` Well, `1` Wellbore, `2` DataObject |
| `Action` | `0` Read, `1` Create, `2` Update, `3` Delete |
| `Effect` | `0` Allow, `1` Deny |
| `Inherit` | Whether a Well/Wellbore rule applies to descendants |

Example: grant Alice read access to a Well and its descendants:

```javascript
db.AclRules.replaceOne(
  { Username: "alice", ResourceId: "WELL-UID", ResourceType: 0, Action: 0 },
  {
    Username: "alice",
    ResourceId: "WELL-UID",
    ResourceType: 0,
    Action: 0,
    Effect: 0,
    Inherit: true
  },
  { upsert: true }
);
```

Example: explicitly deny Alice updates to one DataObject:

```javascript
db.AclRules.replaceOne(
  { Username: "alice", ResourceId: "OBJECT-UUID", ResourceType: 2, Action: 2 },
  {
    Username: "alice",
    ResourceId: "OBJECT-UUID",
    ResourceType: 2,
    Action: 2,
    Effect: 1,
    Inherit: false
  },
  { upsert: true }
);
```

## Safe unique-index rollout

The store creates the unique compound index
`UX_AclRules_Username_Resource_Type_Action` when the ACL rule store is
constructed. The store now performs a read-only duplicate-key aggregation before index creation; if conflicts are found, startup fails with the number and details of conflicting key groups, and no ACL documents are changed. Before deploying this version against an existing database, run
the read-only duplicate preflight below against the configured WITSML database.
Do not rely on application startup as a duplicate detector: MongoDB rejects a
unique index when conflicting documents already exist.

The index key is the ACL identity tuple
`(Username, ResourceId, ResourceType, Action)`. `Effect` and `Inherit`
are deliberately not part of the key: changing either value updates the same
rule rather than creating a second rule.

Run this in `mongosh` (or the compatible MongoDB shell) after selecting the
WITSML database:

```javascript
const duplicateAclKeys = db.AclRules.aggregate([
  {
    $group: {
      _id: {
        Username: "$Username",
        ResourceId: "$ResourceId",
        ResourceType: "$ResourceType",
        Action: "$Action"
      },
      Count: { $sum: 1 },
      DocumentIds: { $push: "$_id" }
    }
  },
  { $match: { Count: { $gt: 1 } } },
  { $sort: { Count: -1 } }
]).toArray();

print("Duplicate ACL key groups: " + duplicateAclKeys.length);
duplicateAclKeys.forEach(function (item) {
  printjson(item);
});
```

This query is read-only. If it reports one or more groups:

1. Stop the rollout; do not start/restart the application with the new ACL
   store, because its unique-index creation will fail.
2. Preserve a database backup and export the reported documents (including
   their `_id`, `Effect`, and `Inherit` values) for an administrator to
   review.
3. Resolve each conflicting key explicitly according to the intended access
   policy. Do not automatically delete, merge, or choose Allow/Deny: doing so
   could silently change effective permissions. Retain one approved rule per
   key only after the administrator has decided the intended `Effect` and
   `Inherit` values.
4. Re-run the preflight. Continue only when it reports zero duplicate groups.

After a clean preflight, deploy/start the application. The store creates the
unique index. Verify it with:

```javascript
db.AclRules.getIndexes().filter(function (index) {
  return index.name === "UX_AclRules_Username_Resource_Type_Action";
});
```

The returned index must have `unique: true` and the four key fields in the
order `Username`, `ResourceId`, `ResourceType`, `Action`. Keep this
preflight in the deployment runbook for existing installations; it does not
modify ACL data or create the index itself.

## Evaluation rules

- A rule on the requested object always applies to that object.
- A rule on Well or Wellbore applies to descendants only when `Inherit: true`.
- An applicable `Deny` overrides every applicable `Allow`.
- Once an object hierarchy is ACL-scoped, an authenticated user needs an applicable `Allow` for the requested action; otherwise access is denied.
- A hierarchy with no applicable ACL rule retains the legacy authorization behavior.
- SOAP query results, ETP object reads/listings/counts, cascade deletes, channel streaming and Store Notification delivery are checked server-side.

## Hierarchy scope

For WITSML 1.3.1/1.4.1, the resolver follows the hierarchical ETP URI chain. For WITSML 2.0 flat object URIs, the Store resolves the stored object's `Wellbore` or direct `Well` data-object reference. When a Wellbore reference is present, it loads the referenced Wellbore and resolves its `Well` reference. This produces the effective DataObject → Wellbore → Well hierarchy for those object types whose persisted model exposes these standard references. Object types without a Well/Wellbore reference can only inherit from hierarchy levels available in their URI.
