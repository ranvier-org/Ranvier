# HMAC algorithm-name controls

The new key-service normalizer uses the WebCrypto Level 2 Editor's Draft dated
22 October 2026, sections 9 and 18.4.4: algorithm names match ASCII letters
case-insensitively. Other characters and significant leading/trailing characters
remain part of the name. The legacy `CryptoContract` helpers are unchanged by
this correction.

An explicitly supplied null string converts to the DOMString `null`; it is an
unknown algorithm name and produces `NotSupportedError`. A missing required hash
or missing algorithm value remains `TypeError`. This follows the DOMString
conversion and algorithm-normalization stages separately.

Sources: [WebCrypto Level 2](https://w3c.github.io/webcrypto/),
[Web IDL DOMString conversion](https://webidl.spec.whatwg.org/#es-DOMString).

The shared suite covers 30 independent name/hash cases through generation and
import, plus ignored hash/length members during signing and verification. The
controls include ASCII upper/lower/mixed case, U+017F, U+0131, U+212A, accented
letters, NBSP, BOM, ASCII whitespace, embedded NUL, explicit null and empty strings.
An actual Chromium 156 raw-WebCrypto control supplies the paired observations;
the normative target remains the cited specifications.
