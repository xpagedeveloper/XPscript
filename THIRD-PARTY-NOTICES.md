# Third party reference notices

XPscript uses the following open source projects as implementation references when mapping the HCL Notes/Domino API. Their source code is not copied into XPscript and neither project is a runtime dependency of an XPscript application.

## HCL Domino JNX

Copyright 2019-2021 HCL.

The [Domino JNX project](https://github.com/HCL-TECH-SOFTWARE/domino-jnx) is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). XPscript uses its public API design and native API mapping as a reference for Notes MIME behavior, stream flags, and lifecycle handling.

## Domino JNA

Copyright Klehmann and contributors.

The [Domino JNA project](https://github.com/klehmann/domino-jna) is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). XPscript uses its public implementation as a reference for MIME stream serialization, MIME4J integration boundaries, and Notes item writeback behavior.

No JNX or Domino JNA code is distributed by XPscript. If code from either project is copied into XPscript in the future, the applicable copyright notices, license text, and required NOTICE material must be included with that distribution.
