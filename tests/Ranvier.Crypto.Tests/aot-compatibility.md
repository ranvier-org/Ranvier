# Crypto diagnostic display compatibility

The AOT correction changes the presentation of `CryptoOutcome.ToString()`:
`Value` omits its payload, and `Unsupported: reason` includes the existing reason.
This intentionally replaces generated structural formatting. The union cases,
payloads, errors and provider operations retain their behavior.

Normal compilation previously printed values such as `Value "private-payload"`.
The reflection-free compiler mode could instead fall back to CLR type names.
The explicit displays now have the same result under both compilation modes.
`DigestAlgorithm` and `SubtleOperation` explicitly retain their four and twelve
normal case names respectively.

The graph-proven union types point debugger displays at their explicit `ToString`
labels. This prevents generated structural debugger helpers from bringing the
reflective formatter back into an ordinary-built dependency library.

New unpublished key-contract displays use the case, type or algorithm name, with
key buffers and JWK member values omitted. The shared tests assert these labels
using marked private input, alongside all existing operational tests.

Legacy crypto diagnostic construction uses direct string composition and invariant
decimal formatting. Before/after controls compare exact error names, details,
Message text, validation precedence and Unsupported reasons across invariant,
en-US, de-DE and ar-SA cultures. No diagnostic is suppressed to make AOT pass.
