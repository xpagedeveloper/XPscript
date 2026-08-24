from pathlib import Path
import subprocess

# Copy/move changes are already committed on this branch. Reuse the existing
# prepare workflow once more to migrate Path from a static API to a normal
# XPscript class instance created with New Path(...).
exec(Path('scripts/_patch_path_object.py').read_text(), {'__name__': '__main__'})

# The workflow stages the filesystem runtime/preprocessor/sample/docs paths.
# XPScriptTranspiler is the one additional compiler file touched by this patch.
subprocess.run(['git', 'add', 'src/XPScript.Compiler/XPScriptTranspiler.cs'], check=True)
