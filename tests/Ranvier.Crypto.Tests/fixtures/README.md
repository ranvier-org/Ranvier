# HMAC primary fixtures

crypto-hmac-rfc2202.json and crypto-hmac-rfc4231.json are byte-identical copies of the reviewed data fixtures from native research commit fc805617b04f278225f92085e1f98dbf5e17d7a8 (retained source archive bd22bdf). Their embedded source links cite RFC Editor 2202 section 3 and RFC Editor 4231 section 4.

SHA256: RFC2202 c6463f6da393bb480eb93e6c6f7ebbc1fa1755350599abd5b6e3aa13fdd2462e; RFC4231 7a5f77e1d441412aa9434271b479a6ea0d3c6f2982163f2269d690967b8a6b1e.

hmac-provider-rows.json only flattens those published values into 29 provider rows. Prefix-only published cases remain prefix comparisons; no missing MAC suffix is generated. Provider computation is tested through the public managed/browser facade, separately from fixture provenance.
