# File I/O TOCTOU review

XPscript filesystem operations use OS/.NET path semantics and intentionally do not create an implicit sandbox.

## FileCopy

`FileCopy` uses an already-open source handle as the security boundary. Unix opens with `O_NOFOLLOW`. Windows opens with `FILE_FLAG_OPEN_REPARSE_POINT` and validates the opened handle before reading. The destination is staged as a regular file and revalidated before publication.

## Kill, Name and RmDir

`Kill`, `Name` and `RmDir` operate on filesystem directory entries. Their link checks are repeated immediately before the final operation, but a pathname can still be exchanged by another process between a check and the operation.

The security invariant is that the final delete/rename operation must not follow a substituted symbolic link or reparse point and mutate the link target. If another process wins the race, the operation may fail or may operate on the substituted directory entry according to OS filesystem semantics. Applications that require ownership or directory confinement must provide that policy above these general-purpose filesystem APIs.

The permanent `File IO Entry Symlink Safety` workflow verifies on Windows, Ubuntu and macOS that `Kill`, `Name` and `RmDir` do not modify symlink targets when their operand entries are links.
