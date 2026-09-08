# NotesDXLImporter native audit

The XPscript NotesDXLImporter runtime is backed by the Domino C API DXL importer handle and DXLImport.

Verified C API importer properties in DXL_IMPORT_PROPERTY are IDs 1 through 12. XPscript currently exposes IDs 1 through 9. IDs 10 through 12 are result log comment, result log, and imported note list.

The C API defaults are authoritative. DXLCreateImporter defaults include ACLImportOption=IGNORE, DesignImportOption=IGNORE, DocumentsImportOption=CREATE, CreateFullTextIndex=FALSE, ReplaceDbProperties=FALSE, InputValidationOption=AUTO, ReplicaRequiredForReplaceOrUpdate=TRUE, ExitOnFirstFatalError=TRUE and UnknownTokenLogOption=FATALERROR.

Current XPscript CreateDxlImporter overrides DesignImportOption and DocumentsImportOption to REPLACE_ELSE_CREATE and ReplicaRequiredForReplaceOrUpdate to FALSE. Those overrides are not NotesDXLImporter defaults and should be removed before the runtime is considered semantically aligned.

CompileLotusScript is part of the higher-level NotesDXLImporter API but is not present in the verified public Domino C API DXL_IMPORT_PROPERTY enum. Do not add it to XPscript unless a supported C API or JNX/JNI implementation is verified.

Native-backed follow-up candidates are LogComment (property 10), Log (property 11), ImportedNoteCount and imported note ID iteration through the imported ID table (property 12).
