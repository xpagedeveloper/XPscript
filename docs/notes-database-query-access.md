# NotesDatabase.QueryAccess

`NotesDatabase.QueryAccess` computes the effective Domino ACL access for the current Notes user or for an explicitly supplied user.

XPscript uses Domino's native ACL engine. It does not enumerate ACL entries and try to reproduce Domino precedence rules in managed code.

## Current user

When no user name is supplied, the lookup uses the current `NotesSession.UserName`.

```xpscript
Dim access As NotesDatabaseAccess
Set access = db.QueryAccess()

Print access.UserName
Print access.LevelName
```

## Any user

Pass a Domino user name to evaluate that user against the database ACL.

```xpscript
Dim access As NotesDatabaseAccess
Set access = db.QueryAccess("CN=Jane Doe/O=Example")

Print access.Level
Print access.LevelName
```

An empty string is treated like the omitted argument and uses the current Notes user.

For hierarchical users, use a fully distinguished Domino name when possible.

## Effective access level

`Level` uses the native Domino ACL level values:

| Level | LevelName |
| ---: | --- |
| 0 | `NoAccess` |
| 1 | `Depositor` |
| 2 | `Reader` |
| 3 | `Author` |
| 4 | `Editor` |
| 5 | `Designer` |
| 6 | `Manager` |

`Level` is the effective result returned by Domino after applying the database ACL to the user's expanded names list. Domino's own precedence rules therefore apply. An explicit person ACL entry can, for example, take precedence over a group entry with a different access level.

## Names and groups

`NamesList` contains the complete `NAMES_LIST` produced by Domino for the requested user.

The first entry is the resolved user name. All following entries are groups of which the user is a member, including indirect or nested group membership. `Groups` exposes those group entries directly.

```xpscript
Forall groupName In access.Groups
    Print CStr(groupName)
End Forall
```

This provides an arbitrary-user equivalent of the group-expansion information commonly obtained for the current user with formula-language name-list functions.

## Roles

`Roles` contains the effective ACL role or privilege names returned by `ACLLookupAccess` for the user.

```xpscript
Forall roleName In access.Roles
    Print CStr(roleName)
End Forall
```

The role list is the result of Domino ACL evaluation. XPscript does not infer roles by manually combining ACL entries.

## Access flags

`AccessFlags` exposes the native ACL modifier flags as an integer. Convenience Boolean properties expose the documented Domino ACL flags:

| Property | Meaning |
| --- | --- |
| `CannotCreateDocuments` | Author-level create restriction is set |
| `CannotDeleteDocuments` | Delete restriction is set |
| `CanCreatePersonalAgents` | Personal-agent flag is set |
| `CanCreatePersonalFolders` | Personal-folder flag is set |
| `CanCreateSharedFolders` | Shared-folder/view creation flag is set |
| `CanCreateLotusScriptAgents` | LotusScript/Java agent creation flag is set |
| `IsPublicReader` | Public-reader flag is set |
| `IsPublicWriter` | Public-writer flag is set |
| `CannotReplicateOrCopy` | Replicate/copy restriction is set |
| `IsPersonEntry` | Result carries the person-entry flag |
| `IsGroupEntry` | Result carries the group-entry flag |
| `IsServerEntry` | Result carries the server-entry flag |

Domino documents that only some modifier flags are meaningful for each access level. Consumers should therefore interpret these flags together with `Level`, rather than assuming every Boolean is meaningful for every level.

## Parent and lifetime

`Parent` returns the `NotesDatabase` used for the lookup.

`NotesDatabaseAccess` follows the standard XPscript Notes object lifetime model and supports `Recycle()` and `IsRecycled`.

```xpscript
Call access.Recycle()
```

## Native implementation

The implementation is backed by these HCL Domino C API operations:

- `NSFBuildNamesList` builds the user and nested-group `NAMES_LIST`
- `NSFDbReadACL` reads the database ACL into memory
- `ACLLookupAccess` computes the effective access level, flags, and role list
- `ListGetText` reads the role-name text list returned by the ACL lookup
- `OSLockObject`, `OSUnlockObject`, and `OSMemFree` manage Domino-owned memory

All native handles returned during the lookup are released before `QueryAccess` returns. The resulting `NotesDatabaseAccess` object contains an immutable managed snapshot of the access calculation.

## Example

```xpscript
Dim session As NotesSession
Dim db As NotesDatabase
Dim access As NotesDatabaseAccess

Set session = New NotesSession()
Set db = session.OpenDatabase("", "apps/customers.nsf")
Set access = db.QueryAccess("CN=Jane Doe/O=Example")

Print access.UserName & ": " & access.LevelName

Forall groupName In access.Groups
    Print "Group: " & CStr(groupName)
End Forall

Forall roleName In access.Roles
    Print "Role: " & CStr(roleName)
End Forall

Call access.Recycle()
Call db.Recycle()
Call session.Recycle()
```
