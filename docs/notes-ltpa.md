# NotesLTPA

`NotesLTPA` generates Domino-key LTPA tokens for a configured SSO configuration. It reads the configuration from `names.nsf` on the supplied server using the `($WebSSOConfigs)` view.

```xpscript
Dim session As NotesSession
Dim ltpa As NotesLTPA
Dim token As String

Set session = New NotesSession("C:\\Program Files\\HCL\\Notes")
Set ltpa = New NotesLTPA(session, "CN=Domino01/O=Example", "Example:LtpaToken")
token = ltpa.GenerateToken("CN=Alice Example/O=Example")
Print token
```

The configuration name follows Domino SSO naming rules. For Internet Site configurations it is normally `Organization:TokenName`; otherwise it is the token name. The configuration must contain `LTPA_DominoSecret` and a positive `LTPA_TokenExpiration` value.

This implementation supports Domino-key SSO configurations. It does not support SSO secrets imported from WebSphere. The database signer must have the SSO configuration's Owner or Administrator role, as required by Domino for reading the secret.

`GenerateToken` uses Domino LMBCS encoding for the user name and returns standard Base64 token text. Treat the result as a bearer credential: do not log it, persist it unnecessarily, or expose token generation to untrusted callers. The generated token format uses the SHA-1 digest required by Domino's legacy LTPA format for compatibility; it is not a general-purpose password or message-authentication API.

See [`notes-ltpa-runtime-test.xps`](../samples/notes-ltpa-runtime-test.xps) for executable coverage.
