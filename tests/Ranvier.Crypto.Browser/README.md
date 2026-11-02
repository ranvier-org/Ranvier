# Browser HMAC provider conformance

This project runs the same unfiltered HMAC checks as `Ranvier.Crypto.Tests` through
actual `crypto.subtle` calls. It also checks a genuine insecure HTTP origin.
It is excluded from ordinary .NET builds and test discovery.

From the repository root:

```sh
dotnet tool restore
dotnet fickl tests/Ranvier.Crypto.Browser/Browser.fsproj -e .js -o artifacts/crypto-browser -c Release --noCache
npm ci --prefix tests/Ranvier.Crypto.Browser
npm exec --prefix tests/Ranvier.Crypto.Browser -- playwright install chromium-headless-shell
node tests/Ranvier.Crypto.Browser/browser.mjs artifacts/crypto-browser tests/Ranvier.Crypto.Tests/fixtures/hmac-provider-rows.json artifacts/crypto-browser-result.json
```

The runner serves localhost and `local.test`, maps only the latter to loopback,
and records the actual secure-context flag and failures for each page. It does
not replace `window.crypto` or override security state. A failure in either
context makes the command fail. Playwright is pinned by the lock file; browser
installation and output paths can use the usual Playwright environment settings.

The initial proof used Playwright 1.64.1 and Chromium 156. The secure context ran
259 assertions in 41 groups; the insecure context ran two capability assertions.
This is HMAC-slice evidence, not completion of Core #289 or its full browser profile.
