# HMAC provider conformance

This is the first implementation slice for Core #289. It exercises the public
`SubtleCryptoService` through the .NET provider. The browser project links the same
checks and published vectors. The remaining algorithm and operation families are
not implemented by this slice.

Run the complete native suite:

```sh
dotnet test tests/Ranvier.Crypto.Tests/Ranvier.Crypto.Tests.fsproj -c Release
```

The project's `RunCryptoConformance` target executes the program after `VSTest`;
success requires its actual assertion report and exit code, not test discovery.
The project is included in `Ranvier.slnx` and the build script's test-project scan.

Publish and execute the same checks with NativeAOT:

```sh
dotnet publish tests/Ranvier.Crypto.Tests/Ranvier.Crypto.Tests.fsproj -c Release -r win-x64 -f net10.0 -p:RanvierCryptoAot=true -p:RanvierAotClean=true -p:TrimmerSingleWarn=false -o artifacts/crypto-aot
artifacts/crypto-aot/Ranvier.Crypto.Tests.exe --expect-aot
```

Use the matching RID and executable name on another platform. The app-scoped
`RanvierCryptoAot` switch avoids setting `PublishAot` on the shared contract's
netstandard target. `--expect-aot` fails if dynamic code is available.

The initial Windows JIT, NativeAOT executable and Chromium runs each completed
259 assertions in 41 groups. NativeAOT static analysis remains under investigation:
the executable passes, but a strict warning-as-error publish reports FSharp.Core
formatting/reflection reachability. This is not a clean trimming qualification.
No warning suppression or test exclusion is part of the project.

Service disposal rejects new admission with `CryptoException("InvalidStateError", ...)`
on both providers. This is the disposable wrapper's lifetime rule; browser
WebCrypto itself has no disposal method. Existing `CryptoService` is unchanged.
