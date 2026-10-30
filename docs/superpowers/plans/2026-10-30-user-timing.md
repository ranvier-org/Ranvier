# H02 shared user timing

Tracking: https://github.com/ranvier-org/Ranvier.WebCore/issues/109

Implement the selected Performance.now/timeOrigin/mark/measure facade after the
Core/Views 17 cohort. Core DOM alpha.18 and Views 0.1.8/backend alpha.8 are allocated;
the producer remains Native 17. No generated DOM or native ABI change is needed.

- [x] Define clock, portable detail, returned entry, mark/measure options and service contracts.
- [x] Prove duplicate-name resolution, stable ordering, detail copies, legal option combinations,
      selective clears, reset and concurrent names with an injected deterministic clock.
- [x] Implement a synchronized .NET timeline using Stopwatch; share its domain per host.
- [x] Delegate the browser provider to the document realm's real Performance object and test
      active-document ownership, service disposal, interoperability and detail cloning.
- [x] Run the same fixture under Windows JIT, NativeAOT and actual Chromium, plus deterministic
      clock and native concurrency tests. Preserve exact counts and exit codes.
- [x] Run applicable repository gates, inspect public API and request independent review.

Times are milliseconds; timeOrigin is Unix epoch milliseconds and remains stable when entries
are cleared. Named endpoints select the matching mark with the latest timestamp, using insertion
order on ties, as verified against Chromium's actual Performance buffer. Entries are snapshots
ordered by startTime and insertion order on ties. Duplicate marks/measures remain distinct.
Portable details are acyclic null/bool/number/string/array/object trees, copied at insertion and
when returning facade snapshots. Browser mark/measure operations use its real entry buffer.
Null object fields reject with DataCloneError before tuple destructuring and preserve the buffer.
Both providers validate measure name, options and copied portable detail before resolving endpoints;
combined-invalid inputs follow that facade order. The shared fixture has 180 checks, its deterministic
run has 181, and native lifetime/frame-domain/concurrency coverage has 25 checks plus 500 marks.
The native domain is the managed host frame/timer clock with a fixed Unix-millisecond origin
captured at host construction. DOM Event.timeStamp correspondence is unverified and is not
claimed by this facade. The browser domain is the active document realm's Performance object.
Legacy navigation-timing names have no shared native meaning and are explicitly unsupported.
Observers, resource/navigation entries and structured-clone capabilities beyond portable
details remain outside H02. Numeric explicit timestamps, duration, omitted endpoints, error
atomicity and clear semantics remain in scope.

Source: W3C User Timing https://www.w3.org/TR/user-timing/ and pinned WebCore
Source/WebCore/page/PerformanceUserTiming.cpp and PerformanceMark.cpp.
