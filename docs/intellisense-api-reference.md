# Machine-readable IntelliSense API reference

This reference is the canonical machine-readable supplement for public runtime APIs that are documented in topical pages. The VS Code catalog generator scans this file together with the other XPscript documentation.

Every public runtime object, function, method, or property that is not already represented by a machine-readable five-column row in its authoritative reference must be added here in the same change that introduces the API. One public member occupies one row. Do not group member names in a cell.

The required columns are `Member`, `Syntax`, `Parameters`, `Description`, and `Example`. The example must point to an existing executable `.xps` sample or demo. This format is part of the XPscript documentation contract and is validated by CI.

## NotesSession additions

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `NotesSession.HashPassword` | `session.HashPassword(password)` | `password`: plaintext password. | Returns a native Domino password digest. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.VerifyPassword` | `session.VerifyPassword(password, hashedPassword)` | `password`: plaintext password; `hashedPassword`: Domino digest. | Returns `True` when the password matches the native Domino digest. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.AddressBooks` | `session.AddressBooks` | none | Returns the Domino Directories and Personal Address Books known to the session. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.GetEnvironmentString` | `session.GetEnvironmentString(name [, system])` | `name`: environment variable; optional `system`: use exact system name. | Reads a string from the active Notes/Domino environment. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.GetEnvironmentValue` | `session.GetEnvironmentValue(name [, system])` | `name`: environment variable; optional `system`: use exact system name. | Reads a numeric Notes/Domino environment value. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |
| `NotesSession.SetEnvironmentVar` | `session.SetEnvironmentVar(name, value [, system])` | `name`, `value`, optional `system`. | Creates or updates a Notes/Domino environment variable. | [notes-session-full-runtime-test.xps](../samples/notes-session-full-runtime-test.xps) |

## Notes MIME

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `NotesMIMEEntity` | `Dim entity As NotesMIMEEntity` | none | Native Notes/Domino MIME entity wrapper. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentType` | `entity.ContentType` | none | Returns the MIME content type. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentSubType` | `entity.ContentSubType` | none | Returns the MIME content subtype. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.Charset` | `entity.Charset` | none | Returns the MIME charset. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentAsText` | `entity.ContentAsText` | none | Returns decoded entity content as text. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentID` | `entity.ContentID` | none | Returns the MIME Content-ID value. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.ContentLocation` | `entity.ContentLocation` | none | Returns the MIME Content-Location value. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsMultipart` | `entity.IsMultipart` | none | Reports whether the entity is multipart. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsDiscretePart` | `entity.IsDiscretePart` | none | Reports whether the entity is a discrete MIME part. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.IsMessagePart` | `entity.IsMessagePart` | none | Reports whether the entity is a message MIME part. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetFirstChildEntity` | `entity.GetFirstChildEntity()` | none | Returns the first child MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNextSibling` | `entity.GetNextSibling()` | none | Returns the next sibling MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNextEntity` | `entity.GetNextEntity([search])` | optional traversal `search`. | Returns the next MIME entity using the supported traversal mode. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetChildren` | `entity.GetChildren()` | none | Returns child MIME entities. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.CreateChildEntity` | `entity.CreateChildEntity([nextSibling])` | optional sibling entity. | Creates a child MIME entity. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetNthHeader` | `entity.GetNthHeader(name [, occurrence])` | header `name`; optional one-based `occurrence`. | Returns a MIME header. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.SetContentFromText` | `entity.SetContentFromText(stream, contentType, encoding)` | source `stream`, `contentType`, transfer `encoding`. | Replaces entity content from text. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.SetContentFromBytes` | `entity.SetContentFromBytes(stream, contentType, encoding)` | source `stream`, `contentType`, transfer `encoding`. | Replaces entity content from bytes. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetContentAsText` | `entity.GetContentAsText(stream)` | destination `stream`. | Writes decoded text content to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetContentAsBytes` | `entity.GetContentAsBytes(stream)` | destination `stream`. | Writes decoded bytes to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEEntity.GetEntityAsText` | `entity.GetEntityAsText(stream)` | destination `stream`. | Writes the serialized MIME entity to a stream. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEHeader.GetParamVal` | `header.GetParamVal(name)` | parameter `name`. | Reads a MIME header parameter. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |
| `NotesMIMEHeader.SetParamVal` | `header.SetParamVal(name, value)` | parameter `name` and `value`. | Sets a MIME header parameter. | [notes-mime-entity-surface.xps](../samples/notes-mime-entity-surface.xps) |

## SystemInventory

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `SystemInventory` | `Dim inventory As New SystemInventory` | none | Creates a read-only host inventory object. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetSnapshot` | `inventory.GetSnapshot()` | none | Returns a complete `SystemInventorySnapshot`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetSystemInfo` | `inventory.GetSystemInfo()` | none | Returns `SystemInventorySystemInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetCpuInfo` | `inventory.GetCpuInfo()` | none | Returns `SystemInventoryCpuInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetMemoryInfo` | `inventory.GetMemoryInfo()` | none | Returns `SystemInventoryMemoryInfo`. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetPhysicalDisks` | `inventory.GetPhysicalDisks()` | none | Returns physical disk inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetVolumes` | `inventory.GetVolumes()` | none | Returns filesystem volume inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetGraphicsAdapters` | `inventory.GetGraphicsAdapters()` | none | Returns graphics adapter inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetNetworkAdapters` | `inventory.GetNetworkAdapters()` | none | Returns network adapter inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetInstalledApplications` | `inventory.GetInstalledApplications()` | none | Returns installed application inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.GetInstalledPackages` | `inventory.GetInstalledPackages()` | none | Returns installed package inventory. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.FindInstalledSoftware` | `inventory.FindInstalledSoftware(name)` | software `name` search text. | Finds installed software by name or package identifier. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventory.IsSoftwareInstalled` | `inventory.IsSoftwareInstalled(name)` | exact software `name` or package identifier. | Returns `True` when matching software is installed. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.MachineName` | `systemInfo.MachineName` | none | Returns the host machine name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.OperatingSystem` | `systemInfo.OperatingSystem` | none | Returns the operating-system name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.Manufacturer` | `systemInfo.Manufacturer` | none | Returns the system manufacturer when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventorySystemInfo.Model` | `systemInfo.Model` | none | Returns the system model when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryCpuInfo.Name` | `cpu.Name` | none | Returns the processor name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryCpuInfo.LogicalProcessors` | `cpu.LogicalProcessors` | none | Returns the logical processor count. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryMemoryInfo.TotalPhysicalMemory` | `memory.TotalPhysicalMemory` | none | Returns total physical memory in bytes. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `SystemInventoryMemoryInfo.AvailablePhysicalMemory` | `memory.AvailablePhysicalMemory` | none | Returns available physical memory in bytes. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Name` | `software.Name` | none | Returns the installed software name. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Version` | `software.Version` | none | Returns the installed software version. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
| `InstalledSoftwareInfo.Publisher` | `software.Publisher` | none | Returns the software publisher when available. | [systeminventory-runtime-test.xps](../samples/systeminventory-runtime-test.xps) |
