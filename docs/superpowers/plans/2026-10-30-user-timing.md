# H02 shared user timing

Tracking: https://github.com/ranvier-org/Ranvier.WebCore/issues/109

Implement the selected Performance.now/timeOrigin/mark/measure facade on public
Core alpha.12 and Views 0.1.3 bases. No generated DOM or native ABI change is needed.

- [ ] Define clock, portable detail, returned entry, mark/measure options and service contracts.
- [ ] Prove duplicate-name resolution, stable ordering, detail copies, legal option combinations,
      selective clears, reset and concurrent names with an injected deterministic clock.
- [ ] Implement a synchronized .NET timeline using Stopwatch; share its domain per host.
- [ ] Delegate the browser provider to the document realm's real Performance object and test
      active-document ownership, service disposal, interoperability and detail cloning.
- [ ] Run the same fixture under Windows JIT, NativeAOT and actual Chromium, plus deterministic
      clock and native concurrency tests. Preserve exact counts and exit codes.
- [ ] Run applicable repository gates, inspect public API and request independent review.

Times are milliseconds; timeOrigin is Unix epoch milliseconds and remains stable when entries
are cleared. Named endpoints select the matching mark with the latest timestamp, using insertion
order on ties, as verified against Chromium's actual Performance buffer. Entries are snapshots
ordered by startTime and insertion order on ties. Duplicate marks/measures remain distinct.
Portable details are acyclic null/bool/number/string/array/object trees, copied at insertion and
when returning facade snapshots. Browser mark/measure operations use its real entry buffer.
Legacy navigation-timing names have no shared native meaning and are explicitly unsupported.
Observers, resource/navigation entries and structured-clone capabilities beyond portable
details remain outside H02. Numeric explicit timestamps, duration, omitted endpoints, error
atomicity and clear semantics remain in scope.

Source: W3C User Timing https://www.w3.org/TR/user-timing/ and pinned WebCore
Source/WebCore/page/PerformanceUserTiming.cpp and PerformanceMark.cpp.
