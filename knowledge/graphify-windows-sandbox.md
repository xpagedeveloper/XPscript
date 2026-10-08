# Graphify on Windows

The installed `graphify.exe` may fail inside the agent sandbox with `uv trampoline failed to canonicalize script path` even when it works in the user's terminal. On 2026-10-08 the same executable successfully ran `graphify query` outside the sandbox. Use the normal escalation/approval mechanism to retry the prescribed query or AST-only `graphify update .` outside the sandbox when this exact launcher error occurs. Do not treat the error as evidence that graphify is missing, reinstall it, or change its installation merely to bypass the sandbox.
