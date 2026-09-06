# NotesSession / NotesDocument / NotesDatabase surface gaps

This checklist tracks HCL Domino Designer 14.5.1 LotusScript compatibility beyond the XPscript members that are already exposed and covered by the runtime regression samples.

The automated `NotesFullRuntimeSurfaceAudit` currently verifies the generated XPscript surface for these three classes and flags public members that are missing from the regression samples or contain obvious unsupported/placeholder bodies. It does **not** mean the full HCL class library surface is implemented.

## Current XPscript surface

- NotesSession: 23 public members, fulltest-covered.
- NotesDocument: 55 public members, fulltest-covered across `notes-full-domino-runtime-test.xps` and `notes-document-metadata-runtime-test.xps`.
- NotesDatabase: 46 public members, fulltest-covered.
- No obvious `NotImplementedException`, `NotSupportedException`, `Unsupported`, or constant-placeholder bodies were found by the generated-runtime audit.

## NotesSession — HCL members not currently exposed

### Properties

- [ ] AddressBooks
- [ ] CurrentAgent
- [ ] CurrentDatabase
- [ ] DocumentContext
- [ ] EffectiveUserName
- [ ] HttpURL
- [ ] International
- [ ] IsOnServer
- [ ] LastExitStatus
- [ ] LastRun
- [ ] NotesURL
- [ ] OrgDirectoryPath
- [ ] SavedData
- [ ] ServerName
- [ ] URLDatabase
- [ ] UserGroupNameList
- [ ] UserNameList
- [ ] UserNameObject

### Methods

- [ ] CreateAdministrationProcess
- [ ] CreateColorObject
- [ ] CreateDateRange
- [ ] CreateDOMParser
- [ ] CreateLog
- [ ] CreateNewsletter
- [ ] CreateRegistration
- [ ] CreateSAXParser
- [ ] CreateTimer
- [ ] CreateXSLTransformer
- [ ] Evaluate
- [ ] FileOpBegin
- [ ] FileOpEnd
- [ ] FreeResourceSearch
- [ ] FreeTimeSearch
- [ ] GetCalendar
- [ ] GetDatabase
- [ ] GetDbDirectory
- [ ] GetDirectory
- [ ] GetEnvironmentString
- [ ] GetEnvironmentValue
- [ ] GetOIDCAccessToken
- [ ] GetPropertyBroker
- [ ] GetUserPolicySettings
- [ ] HashPassword
- [ ] ResetUserPassword
- [ ] Resolve
- [ ] SendConsoleCommand
- [ ] SetEnvironmentVar
- [ ] UpdateProcessedDoc
- [ ] VerifyPassword

### Platform/API-specific members to classify before implementing

- [ ] Initialize — COM-specific in HCL documentation.
- [ ] InitializeUsingNotesUserName — COM-specific in HCL documentation.
- [ ] AdjustPointer / pointer helpers — version/platform-sensitive; validate intended XPscript ABI before exposing.

## NotesDocument — HCL members not currently exposed

### Properties

- [ ] EmbeddedObjects
- [ ] EncryptionKeys
- [ ] EncryptOnSend
- [ ] FolderReferences
- [ ] FTSearchScore
- [ ] HasEmbedded
- [ ] HttpURL
- [ ] IsEncrypted
- [ ] IsNamedDoc
- [ ] IsUIDocOpen
- [ ] LockHolders
- [ ] NameOfDoc
- [ ] NotesURL
- [ ] ParentView
- [ ] SaveMessageOnSend
- [ ] SignOnSend
- [ ] UserNameOfDoc

### Methods / behavior still to compare with HCL

The currently exposed XPscript document surface already includes core item access, save/remove, response handling, folder operations, copy operations, signing, ComputeWithForm, metadata, and rich-text item creation. Continue the HCL method-by-method audit for the remaining methods before adding them; prioritize methods that can be implemented directly with stable Notes C API calls and do not require UI/OLE automation.

## NotesDatabase — HCL members not currently exposed

### Properties

- [ ] ACL
- [ ] ACLActivityLog
- [ ] DelayUpdates
- [ ] EncryptionStrength
- [ ] FolderReferencesEnabled
- [ ] FTIndexFrequency
- [ ] HttpURL
- [ ] IsClusterReplication
- [ ] IsConfigurationDirectory
- [ ] IsCurrentAccessPublicReader
- [ ] IsCurrentAccessPublicWriter
- [ ] IsDesignLockingEnabled
- [ ] IsDirectoryCatalog
- [ ] IsDocumentLockingEnabled
- [ ] IsInMultiDbIndexing
- [ ] IsInService
- [ ] IsLink
- [ ] IsLocallyEncrypted
- [ ] IsMultiDbSearch
- [ ] IsPendingDelete
- [ ] IsPrivateAddressBook
- [ ] IsPublicAddressBook
- [ ] LastFixup
- [ ] LimitRevisions
- [ ] LimitUpdatedBy
- [ ] ListInDbCatalog
- [ ] Managers
- [ ] MaxSize
- [ ] NotesURL
- [ ] ReplicationInfo
- [ ] SizeQuota
- [ ] SizeWarning
- [ ] Type
- [ ] UndeleteExpireTime
- [ ] UnprocessedDocuments

### Methods

- [ ] Compact
- [ ] CompactWithOptions
- [ ] CreateDominoQuery
- [ ] CreateFromTemplate
- [ ] CreateFTIndex
- [ ] CreateOutline
- [ ] CreateReplica
- [ ] CreateView
- [ ] Decrypt
- [ ] EnableFolder
- [ ] Encrypt
- [ ] Fixup
- [ ] FTDomainSearch
- [ ] FTSearchRange
- [ ] GetAllReadDocuments
- [ ] GetAllUnreadDocuments
- [ ] GetDocumentByURL
- [ ] GetForm
- [ ] GetModifiedDocumentsWithOptions
- [ ] GetNamedDocument
- [ ] GetNamedDocCollection
- [ ] GetOption
- [ ] GetOutline
- [ ] GetProfileDocCollection
- [ ] CreateQueryResultsProcessor
- [ ] GetURLHeaderInfo
- [ ] GrantAccess
- [ ] MarkForDelete
- [ ] Open
- [ ] OpenIfModified
- [ ] OpenMail
- [ ] OpenURLDb

## Implementation priorities

1. Prefer members that map directly to stable Domino C API primitives and existing XPscript wrappers.
2. Keep properties that merely expose managed/static guesses out of the public surface unless HCL semantics can be reproduced.
3. Group dependent members with their required classes instead of returning fake values (for example ACL, Calendar, Directory, ReplicationInfo, Outline, QueryResultsProcessor).
4. Treat COM-only, UI-only, OLE, and platform-specific members separately; do not emulate them with misleading placeholders.
5. Add each newly exposed member to the generated-runtime audit and an executable/compile regression sample in the same change.
