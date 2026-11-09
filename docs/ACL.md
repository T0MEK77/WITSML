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

## Evaluation rules

- A rule on the requested object always applies to that object.
- A rule on Well or Wellbore applies to descendants only when `Inherit: true`.
- An applicable `Deny` overrides every applicable `Allow`.
- Once an object hierarchy is ACL-scoped, an authenticated user needs an applicable `Allow` for the requested action; otherwise access is denied.
- A hierarchy with no applicable ACL rule retains the legacy authorization behavior.
- SOAP query results, ETP object reads/listings/counts, cascade deletes, channel streaming and Store Notification delivery are checked server-side.

## Hierarchy scope

The resolver follows the canonical ETP URI parent chain, ordered from the requested object to its Wellbore and Well ancestors. This supports the hierarchical WITSML 1.3.1/1.4.1 URIs. WITSML 2.0 Well, Wellbore and Log URIs in this codebase are flat (for example, `eml://witsml20/Log(uuid)`); their Well/Wellbore links are stored in data-object references, not in URI path segments. Direct DataObject ACL rules work for those flat URIs, but Well/Wellbore inheritance for WITSML 2.0 still requires resolving those stored references and is not complete yet.
