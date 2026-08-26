# NotesDatabase copy and replica ID operations

`NotesDatabase` exposes the database replica ID and can create an independent copy or explicitly change the replica ID of an open database.

## ReplicaID

`NotesDatabase.ReplicaID` is a read-only `String` containing the 16 hexadecimal characters of the database replica ID.

```xpscript
Dim db As NotesDatabase
Set db = session.OpenDatabase("", "apps\customers.nsf")
Print db.ReplicaID
```

The value is read with the Domino C API `NSFDbReplicaInfoGet` function.

## CreateCopy

```xpscript
Dim copy As NotesDatabase
Set copy = db.CreateCopy("", "apps\customers-copy.nsf")
```

Syntax:

```text
NotesDatabase.CreateCopy(server, filePath) As NotesDatabase
```

- `server` is the destination Domino server. Use an empty string for the local Notes/Domino data directory.
- `filePath` is the destination NSF path.
- The source `NotesDatabase` must be open.
- An empty destination path raises runtime error 5.
- A closed source database returns `Nothing`.
- The returned `NotesDatabase` owns the native handle for the newly created database and should be recycled normally.

XPscript maps this operation to `NSFDbCreateAndCopy` with `NOTE_CLASS_ALL` and without `DBCOPY_REPLICA`. This matches LotusScript `NotesDatabase.CreateCopy`: the destination is a copy, not a replica, and receives a new replica ID.

## SetReplicaId

```xpscript
Call db.SetReplicaId("85258D12004A1234")
```

Syntax:

```text
NotesDatabase.SetReplicaId(replicaId)
```

- `replicaId` must contain exactly 16 hexadecimal characters. Hyphens and colons are accepted and removed before validation.
- The database must be open. Calling the method on a closed database raises runtime error 91.
- Invalid replica-ID text raises runtime error 13.

The implementation first reads the database `DBREPLICAINFO` structure with `NSFDbReplicaInfoGet`, changes only its `ID` member, and writes the complete structure back with `NSFDbReplicaInfoSet`. Replication flags, purge interval, cutoff date, and other returned structure data are preserved.

Changing a replica ID changes which databases Domino considers replicas of each other. Use this operation deliberately, especially on databases that already participate in replication.
