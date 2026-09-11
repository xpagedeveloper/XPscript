# Third party reference notices

XPscript uses the following open source projects as implementation references when mapping the HCL Notes/Domino API. Their source code is not copied into XPscript and neither project is a runtime dependency of an XPscript application.

HCL's open source catalog identifies the Domino C API documentation, Domino JNX, and related Domino open source repositories as Apache License 2.0 projects. XPscript keeps the attribution below and links to the full license text. The HCL Notes and Domino products and native runtime libraries remain commercial HCL software and are not redistributed by XPscript.

## HCL Domino C API documentation

Copyright HCL Software.

The [HCL Domino C API documentation](https://opensource.hcltechsw.com/domino-c-api-docs/) is the primary reference for native function signatures, constants, flags, and MIME API behavior. The HCL open source catalog lists the `domino-c-api-docs` repository under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). XPscript does not copy the documentation or HCL C API toolkit binaries into its distribution.

## HCL Domino JNX

Copyright 2019-2021 HCL.

The [Domino JNX project](https://github.com/HCL-TECH-SOFTWARE/domino-jnx) is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). XPscript uses its public API design and native API mapping as a reference for Notes C API implementation

## Domino JNA

Copyright Klehmann and contributors.

The [Domino JNA project](https://github.com/klehmann/domino-jna) is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). XPscript uses its public implementation as a reference for MIME stream serialization and Notes item writeback behavior.

## NuGet dependencies

XPscript also uses the following open source NuGet packages. The versions below are the versions currently declared by the project or resolved in the repository's package assets. When a package is included in a published application, its license and notice files from the package must remain available with that distribution.

### MIT License

The following packages are distributed under the MIT License:

- [Avalonia](https://github.com/AvaloniaUI/Avalonia), including `Avalonia.Desktop`, `Avalonia.FreeDesktop`, `Avalonia.FreeDesktop.AtSpi`, `Avalonia.HarfBuzz`, `Avalonia.Native`, `Avalonia.Remote.Protocol`, `Avalonia.Skia`, `Avalonia.Themes.Fluent`, `Avalonia.Win32`, and `Avalonia.X11` (`12.0.3`)
- [Avalonia.Controls.WebView](https://github.com/AvaloniaUI/Avalonia.Controls.WebView) (`12.0.1`)
- [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) (`2.6.2`)
- [HarfBuzzSharp](https://github.com/mono/SkiaSharp), including its platform native asset packages (`8.3.1.3`)
- [MicroCom.Runtime](https://github.com/AvaloniaUI/MicroCom) (`0.11.4`)
- [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore), including `Microsoft.Data.Sqlite.Core` (`10.0.11`)
- [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient), including its abstractions and logging packages (`7.0.2`)
- [Microsoft.Extensions](https://github.com/dotnet/runtime) caching, dependency injection, logging, options, and primitives packages (`9.0.13` and `10.0.0`)
- [Microsoft.IdentityModel](https://github.com/AzureAD/identitymodel-extensions-for-dotnet) packages (`8.16.0`)
- [Microsoft.CodeAnalysis](https://github.com/dotnet/roslyn), including `Microsoft.CodeAnalysis.CSharp`, `Common`, and `Analyzers` (`4.14.0` and `3.11.0`)
- [Microsoft.SqlServer.Server](https://github.com/dotnet/SqlClient) (`1.0.0`)
- [MimeKit](https://github.com/jstedfast/MimeKit) (`4.17.0`)
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) (`0.50.4`), included in generated applications only when an `Archive` object is created with extended support enabled
- [SkiaSharp](https://github.com/mono/SkiaSharp), including its platform native asset packages (`3.119.4-preview.1.1`)
- [System.Configuration.ConfigurationManager](https://github.com/dotnet/runtime), `System.Diagnostics.EventLog`, `System.IdentityModel.Tokens.Jwt`, `System.Security.Cryptography.Pkcs`, and `System.Security.Cryptography.ProtectedData` (`9.0.13` or `10.0.0`)
- [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) (`0.92.0`)
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) (`18.1.0`)

The MIT license text is available at [opensource.org/licenses/MIT](https://opensource.org/license/mit/).

### Apache License 2.0

- [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw), including `bundle_e_sqlite3`, `core`, `lib.e_sqlite3`, and `provider.e_sqlite3` (`2.1.12`)

The Apache License 2.0 text is available at [apache.org/licenses/LICENSE-2.0](https://www.apache.org/licenses/LICENSE-2.0). SQLite itself is a separate public-domain work; the SQLitePCLRaw package notices apply to the managed package and its native distribution.

### ANGLE license

- [Avalonia.Angle.Windows.Natives](https://github.com/google/angle) (`2.1.25547.20250602`) includes the ANGLE Project license and notices. The package's `LICENSE` file must be retained with any distributed binary using it. The license is a BSD-style permissive license.

### Microsoft software license terms

- [Microsoft.Data.SqlClient.SNI.runtime](https://github.com/dotnet/SqlClient) (`6.0.2`) is distributed with Microsoft's `LICENSE.txt` software license terms. Those terms and any accompanying `ThirdPartyNotices` file must be retained with any published application that contains the SNI native runtime.

The complete package dependency graph can change when package versions are updated. Re-run `dotnet list <project>.csproj package --include-transitive` and refresh this section whenever dependencies change.

## .NET runtime and SDK

XPscript is built on the [.NET platform](https://github.com/dotnet/runtime) and uses the .NET SDK, runtime libraries, ASP.NET Core components, and standard library implementations supplied by Microsoft. These components are generally distributed under the [MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), with additional third-party notices included in the .NET installation and publish output. Self-contained applications must retain the notices shipped with the corresponding .NET runtime used to build them.

The .NET runtime is supplied by the .NET distribution and is not source code copied into this repository. This notice does not grant rights to redistribute HCL Notes/Domino binaries, which remain subject to HCL's commercial product terms.
