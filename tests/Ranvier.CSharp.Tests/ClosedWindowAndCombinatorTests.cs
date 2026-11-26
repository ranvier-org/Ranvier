using Ranvier;
using Ranvier.CSharp;
using Xunit;
using static Ranvier.CSharp.Reactive;

namespace CSharpCallers;

public class ClosedWindowAndCombinatorTests
{
    [Fact]
    public void MemoOnClosesTrackingInsideComputeAndPreservesValueAcrossUnreadyRead()
    {
        using var active = new Graph().Activate();
        var key = Signal(2);
        var gate = Signal(true);
        var pending = AsyncSource<int>();
        var untracked = Signal(100);
        var computeRuns = 0;

        var derived = MemoOn(
            () => gate.Value ? key.Value : pending.Value,
            k =>
            {
                computeRuns++;
                return k * 10 + untracked.Value;
            });

        Assert.Equal(120, derived.Value);
        Assert.Equal(1, computeRuns);

        // Changing a signal read only inside compute does not invalidate MemoOn.
        untracked.Value = 200;
        Assert.Equal(120, derived.Value);
        Assert.Equal(1, computeRuns);

        // Transient pending read surfaces Pending without discarding last key/value.
        gate.Value = false;
        Assert.True(derived.TryValue.IsPending);
        Assert.Equal(1, computeRuns);

        // Recovering to the same settled key restores cached compute result without re-running compute.
        gate.Value = true;
        Assert.Equal(120, derived.Value);
        Assert.Equal(1, computeRuns);

        // Settling on a new key re-runs compute once.
        key.Value = 3;
        Assert.Equal(230, derived.Value);
        Assert.Equal(2, computeRuns);
    }

    [Fact]
    public void MemoOnReadingPendingInsideComputeFailsWithInvalidOperationException()
    {
        using var active = new Graph().Activate();
        var key = Signal(1);
        var neverReady = AsyncSource<int>();
        var broken = MemoOn(() => key.Value, _ => neverReady.Value);

        Assert.Throws<InvalidOperationException>(() => _ = broken.Value);
    }

    [Fact]
    public void MemoOnWithComparerAppliesCustomOutputCutoff()
    {
        using var active = new Graph().Activate();
        var raw = Signal("ada");
        var seen = new List<string>();
        var upper = MemoOn(() => raw.Value, s => s, StringComparer.OrdinalIgnoreCase);

        Effect(() => seen.Add(upper.Value));
        Flush();

        raw.Value = "ADA";
        Flush();
        raw.Value = "grace";
        Flush();

        Assert.Equal(["ada", "grace"], seen);
    }

    [Fact]
    public void OwningMemoOnDischargesOwnedScopeOnlyOnUnequalSettledKeys()
    {
        using var active = new Graph().Activate();
        var raw = Signal(10);
        var gate = Signal(true);
        var pending = AsyncSource<int>();
        var cleanups = new List<int>();
        var seen = new List<string>();

        var owned = OwningMemoOn(
            () => gate.Value ? raw.Value / 10 : pending.Value,
            bucket =>
            {
                OnCleanup(() => cleanups.Add(bucket));
                return bucket == 1 ? "one" : "ONE";
            },
            StringComparer.OrdinalIgnoreCase);

        Effect(() => seen.Add(owned.Value));
        Flush();
        Assert.Equal(["one"], seen);
        Assert.Empty(cleanups);

        // Equal key keeps compute's owned scope intact.
        raw.Value = 15;
        Flush();
        Assert.Empty(cleanups);

        // Pending read keeps owned scope intact until an unequal settled key arrives.
        gate.Value = false;
        Assert.True(owned.TryValue.IsPending);
        Assert.Empty(cleanups);

        gate.Value = true;
        Flush();
        Assert.Empty(cleanups);
        Assert.Equal(["one", "one"], seen);

        // Unequal settled key discharges bucket 1's scope, while case-insensitive comparer suppresses observer wake.
        raw.Value = 20;
        Flush();
        Assert.Equal([1], cleanups);
        Assert.Equal(["one", "one"], seen);

        owned.Dispose();
        Assert.Equal([1, 2], cleanups);
    }

    [Fact]
    public void OwningMemoOnTwoArgCreatesInnerReactiveNodesPerKey()
    {
        using var active = new Graph().Activate();
        var multiplier = Signal(2);
        var innerTick = Signal(5);
        var innerRuns = 0;

        var outer = OwningMemoOn(
            () => multiplier.Value,
            m =>
            {
                var child = Memo(() =>
                {
                    innerRuns++;
                    return m * innerTick.Value;
                });
                return child;
            });

        Assert.Equal(10, outer.Value.Value);
        Assert.Equal(1, innerRuns);

        innerTick.Value = 6;
        Assert.Equal(12, outer.Value.Value);
        Assert.Equal(2, innerRuns);

        var firstChild = outer.Value;
        multiplier.Value = 3;
        Assert.Equal(18, outer.Value.Value);
        Assert.NotSame(firstChild, outer.Value);
    }

    [Fact]
    public void AsyncOnVariantsManageFlightsPreviousValuesAndPolicies()
    {
        var graph = new Graph(GraphOptions.Default.WithDispatcher(new ManualDispatcher()));
        using var active = graph.Activate();

        var query = Signal("a");
        var untrackedBias = Signal(1);
        var flights = new List<(string Key, CancellationToken Token, TaskCompletionSource<int> Reply)>();

        var basic = AsyncOn(
            () => query.Value,
            (key, token) =>
            {
                _ = untrackedBias.Value;
                var reply = new TaskCompletionSource<int>();
                flights.Add((key, token, reply));
                return reply.Task;
            });

        Assert.True(basic.TryValue.IsPending);
        Assert.Single(flights);

        // Untracked signal inside compute does not start a new flight.
        untrackedBias.Value = 2;
        Assert.True(basic.TryValue.IsPending);
        Assert.Single(flights);

        // Unequal key cancels previous flight under default CancelPrevious.
        query.Value = "ab";
        Assert.True(basic.TryValue.IsPending);
        Assert.Equal(2, flights.Count);
        Assert.True(flights[0].Token.IsCancellationRequested);

        flights[1].Reply.SetResult(42);
        Assert.Equal(42, basic.Value);

        // Previous<U> overload + FlightPolicy.FinishCurrent overload.
        var step = Signal(5);
        var prevFlights = new List<(int Step, Previous<int> Prev, TaskCompletionSource<int> Gate)>();
        var accumulated = AsyncOn<int, int>(
            () => step.Value,
            async (delta, previous, token) =>
            {
                var gate = new TaskCompletionSource<int>();
                prevFlights.Add((delta, previous, gate));
                var extra = await gate.Task;
                return await previous.SettledOr(100) + delta + extra;
            },
            FlightPolicy.FinishCurrent);

        Assert.True(accumulated.TryValue.IsPending);
        step.Value = 10;
        // FinishCurrent keeps first flight running without starting second until first settles.
        Assert.Single(prevFlights);

        prevFlights[0].Gate.SetResult(0);
        // Trailing flight launches on next read with previous settled value 105.
        Assert.True(accumulated.TryValue.IsPending);
        Assert.Equal(2, prevFlights.Count);
        prevFlights[1].Gate.SetResult(1);
        Assert.Equal(116, accumulated.Value);

        // Previous<U> without explicit policy + non-Previous with explicit policy.
        var count = Signal(1);
        var withPrevDefault = AsyncOn<int, int>(
            () => count.Value,
            async (n, previous, _) => await previous.SettledOr(0) + n);
        var withPolicyNoPrev = AsyncOn(
            () => count.Value,
            (n, _) => Task.FromResult(n * 10),
            FlightPolicy.KeepLatest);

        Assert.Equal(1, withPrevDefault.Value);
        Assert.Equal(10, withPolicyNoPrev.Value);
        count.Value = 4;
        Assert.Equal(5, withPrevDefault.Value);
        Assert.Equal(40, withPolicyNoPrev.Value);
    }

    [Fact]
    public void OwningAsyncOnManagesSynchronousFlightScopesAcrossAllOverloads()
    {
        var graph = new Graph(GraphOptions.Default.WithDispatcher(new ManualDispatcher()));
        using var active = graph.Activate();

        var key = Signal(1);
        var gate = Signal(true);
        var pending = AsyncSource<int>();
        var cleaned = new List<int>();
        var replies = new List<TaskCompletionSource<int>>();

        var owned = OwningAsyncOn(
            () => gate.Value ? key.Value : pending.Value,
            (k, _) =>
            {
                OnCleanup(() => cleaned.Add(k));
                var tcs = new TaskCompletionSource<int>();
                replies.Add(tcs);
                return tcs.Task;
            });

        Assert.True(owned.TryValue.IsPending);
        Assert.Empty(cleaned);

        // Transient pending read preserves active flight and its owned cleanup.
        gate.Value = false;
        Assert.True(owned.TryValue.IsPending);
        Assert.Empty(cleaned);

        gate.Value = true;
        replies[0].SetResult(10);
        Assert.Equal(10, owned.Value);
        Assert.Empty(cleaned);

        // Unequal key discharges flight 1's synchronous scope before launching flight 2.
        key.Value = 2;
        Assert.True(owned.TryValue.IsPending);
        Assert.Equal([1], cleaned);
        replies[1].SetResult(20);
        Assert.Equal(20, owned.Value);

        // Policy + Previous overloads on OwningAsyncOn.
        var policyCleaned = 0;
        var withPolicy = OwningAsyncOn(
            () => key.Value,
            (k, _) =>
            {
                OnCleanup(() => policyCleaned++);
                return Task.FromResult(k * 3);
            },
            FlightPolicy.KeepLatest);

        var withPrev = OwningAsyncOn<int, int>(
            () => key.Value,
            async (k, prev, _) =>
            {
                OnCleanup(() => { });
                return await prev.SettledOr(0) + k;
            });

        var withPrevAndPolicy = OwningAsyncOn<int, int>(
            () => key.Value,
            async (k, prev, _) =>
            {
                OnCleanup(() => { });
                return await prev.SettledOr(10) + k;
            },
            FlightPolicy.FinishCurrent);

        Assert.Equal(6, withPolicy.Value);
        Assert.Equal(2, withPrev.Value);
        Assert.Equal(12, withPrevAndPolicy.Value);

        key.Value = 5;
        Assert.Equal(15, withPolicy.Value);
        Assert.Equal(1, policyCleaned);
        Assert.Equal(7, withPrev.Value);
        Assert.Equal(17, withPrevAndPolicy.Value);
    }

    [Fact]
    public void StreamOnAndStreamFoldOnSplitKeyReadFromStreamStartup()
    {
        using var active = new Graph().Activate();
        var channelKey = Signal("alpha");
        var gate = Signal(true);
        var pending = AsyncSource<string>();
        var untracked = Signal(99);

        var started = new List<(string Key, CancellationToken Token, StreamSink<int> Sink)>();

        var latest = StreamOn(
            () => gate.Value ? channelKey.Value : pending.Value,
            (k, token) =>
            {
                _ = untracked.Value;
                return StreamSources.Create<int>((sink, _) =>
                {
                    started.Add((k, token, sink));
                    return new TaskCompletionSource().Task;
                });
            });

        var folded = StreamFoldOn(
            100,
            (int acc, int x) => acc + x,
            () => gate.Value ? channelKey.Value : pending.Value,
            (k, token) =>
                StreamSources.Create<int>((sink, _) =>
                {
                    started.Add((k + ":fold", token, sink));
                    return new TaskCompletionSource().Task;
                }));

        Assert.True(latest.TryValue.IsPending);
        Assert.True(folded.TryValue.IsPending);
        Assert.Equal(2, started.Count);

        started[0].Sink.Next(7);
        started[1].Sink.Next(5);
        Assert.Equal(7, latest.Value);
        Assert.Equal(105, folded.Value);

        // Untracked signal change does not restart either stream.
        untracked.Value = 100;
        Assert.Equal(7, latest.Value);
        Assert.Equal(2, started.Count);

        // Items arriving while key read is pending keep folding and surface on recovery to equal key.
        gate.Value = false;
        Assert.True(folded.TryValue.IsPending);
        started[1].Sink.Next(15);
        gate.Value = true;
        Assert.Equal(120, folded.Value);
        Assert.Equal(2, started.Count);

        // Unequal key cancels prior streams and resets fold state to seed (100).
        channelKey.Value = "beta";
        Assert.True(latest.TryValue.IsPending);
        Assert.True(folded.TryValue.IsPending);
        Assert.True(started[0].Token.IsCancellationRequested);
        Assert.True(started[1].Token.IsCancellationRequested);
        Assert.Equal(4, started.Count);

        started[2].Sink.Next(9);
        started[3].Sink.Next(3);
        Assert.Equal(9, latest.Value);
        Assert.Equal(103, folded.Value);
    }

    [Fact]
    public void SelectSupportsFourStageFusedChainsIntermediateCutoffsAndAllFiveSources()
    {
        using var active = new Graph().Activate();
        var input = Signal(10);
        var untracked = Signal(1);
        var stage3Runs = 0;
        var stage4Runs = 0;

        var s1 = input.Select(x => x + untracked.Value - 1); // 10
        var s2 = s1.Select(x => x / 10);                    // 1
        var s3 = s2.Select(x =>
        {
            stage3Runs++;
            return x * 100;
        });
        var s4 = s3.Select(x =>
        {
            stage4Runs++;
            return $"v={x}";
        });

        Assert.Equal("v=100", s4.Value);
        Assert.Equal(1, stage3Runs);
        Assert.Equal(1, stage4Runs);

        // Untracked signal inside selector is ignored.
        untracked.Value = 500;
        Assert.Equal("v=100", s4.Value);
        Assert.Equal(1, stage3Runs);

        // Cutoff at stage 2 (15 / 10 == 1 except untracked is now 500 -> 15 + 499 = 514; let's reset untracked first).
        untracked.Value = 1;
        input.Value = 15; // s1 = 15, s2 = 1 (equal to previous 1!) -> s3 and s4 do not re-run!
        Assert.Equal("v=100", s4.Value);
        Assert.Equal(1, stage3Runs);
        Assert.Equal(1, stage4Runs);

        // Reading an intermediate stage mid-life de-fuses cleanly and agrees with tail.
        Assert.Equal(1, s2.Value);
        input.Value = 25;
        Assert.Equal(2, s2.Value);
        Assert.Equal("v=200", s4.Value);
        Assert.Equal(2, stage3Runs);
        Assert.Equal(2, stage4Runs);

        // Select over AsyncMemo, AsyncSource, and StreamMemo.
        var asyncSource = AsyncSource<int>();
        var fromAsyncSource = asyncSource.Select(x => x * 2);
        Assert.True(fromAsyncSource.TryValue.IsPending);
        var boom = new InvalidOperationException("source failed");
        asyncSource.Fail(boom);
        Assert.Same(boom, Assert.Throws<InvalidOperationException>(() => _ = fromAsyncSource.Value));
        Assert.Same(asyncSource, fromAsyncSource.ErrorOrigin);
        asyncSource.Settle(21);
        Assert.Equal(42, fromAsyncSource.Value);

        var tcs = new TaskCompletionSource<int>();
        var asyncMemo = Async(_ => tcs.Task);
        var fromAsyncMemo = asyncMemo.Select(x => x + 1);
        Assert.True(fromAsyncMemo.TryValue.IsPending);
        tcs.SetResult(9);
        Assert.Equal(10, fromAsyncMemo.Value);

        StreamSink<string>? sink = null;
        var stream = Stream(_ => StreamSources.Create<string>((s, _) =>
        {
            sink = s;
            return new TaskCompletionSource().Task;
        }));
        var fromStream = stream.Select(s => s.Length);
        Assert.True(fromStream.TryValue.IsPending);
        sink!.Next("ranvier");
        Assert.Equal(7, fromStream.Value);
    }

    [Fact]
    public void ZipTwoArgValueTupleAndThreeArgSelectorWorkAcrossAllFiveReactiveTypes()
    {
        using var active = new Graph().Activate();

        // 1. Signal Zip (2-arg ValueTuple destructuring + 3-arg selector)
        var leftSig = Signal(3);
        var rightSig = Signal("items");
        var sigPair = leftSig.Zip(rightSig);
        var sigFormatted = leftSig.Zip(rightSig, (n, label) => $"{n} {label}");
        var (count, unitName) = sigPair.Value;
        Assert.Equal(3, count);
        Assert.Equal("items", unitName);
        Assert.Equal("3 items", sigFormatted.Value);

        // 2. Memo Zip (2-arg + 3-arg)
        var leftMemo = leftSig.Select(x => x * 2);
        var rightMemo = rightSig.Select(s => s.ToUpperInvariant());
        var memoPair = leftMemo.Zip(rightMemo);
        var memoJoined = leftMemo.Zip(rightMemo, (n, s) => $"{s}:{n}");
        var (doubled, upper) = memoPair.Value;
        Assert.Equal(6, doubled);
        Assert.Equal("ITEMS", upper);
        Assert.Equal("ITEMS:6", memoJoined.Value);

        // 3. AsyncSource Zip (left-to-right Pending/Failed precedence + 2-arg + 3-arg)
        var aSrc = AsyncSource<int>();
        var bSrc = AsyncSource<int>();
        var srcPair = aSrc.Zip(bSrc);
        var srcSum = aSrc.Zip(bSrc, (a, b) => a + b);
        Assert.True(srcPair.TryValue.IsPending);
        Assert.True(srcSum.TryValue.IsPending);
        var errRight = new InvalidOperationException("right");
        bSrc.Fail(errRight);
        // Left is still Pending, so left-to-right precedence stays Pending.
        Assert.True(srcSum.TryValue.IsPending);
        aSrc.Settle(10);
        Assert.Same(errRight, Assert.Throws<InvalidOperationException>(() => _ = srcSum.Value));
        bSrc.Settle(32);
        var (av, bv) = srcPair.Value;
        Assert.Equal((10, 32), (av, bv));
        Assert.Equal(42, srcSum.Value);

        // 4. AsyncMemo Zip (2-arg + 3-arg)
        var aAsync = Async(_ => Task.FromResult(4));
        var bAsync = Async(_ => Task.FromResult(5));
        var asyncPair = aAsync.Zip(bAsync);
        var asyncProd = aAsync.Zip(bAsync, (a, b) => a * b);
        var (aa, ba) = asyncPair.Value;
        Assert.Equal((4, 5), (aa, ba));
        Assert.Equal(20, asyncProd.Value);

        // 5. StreamMemo Zip (2-arg + 3-arg)
        StreamSink<int>? s1 = null;
        StreamSink<int>? s2 = null;
        var st1 = Stream(_ => StreamSources.Create<int>((s, _) => { s1 = s; return new TaskCompletionSource().Task; }));
        var st2 = Stream(_ => StreamSources.Create<int>((s, _) => { s2 = s; return new TaskCompletionSource().Task; }));
        var stPair = st1.Zip(st2);
        var stDiff = st1.Zip(st2, (a, b) => a - b);
        Assert.True(stPair.TryValue.IsPending);
        s1!.Next(10);
        Assert.True(stPair.TryValue.IsPending);
        s2!.Next(3);
        var (sv1, sv2) = stPair.Value;
        Assert.Equal((10, 3), (sv1, sv2));
        Assert.Equal(7, stDiff.Value);

        // Cross-graph rejection
        using var otherGraph = new Graph();
        var foreignSig = new Signal<int>(otherGraph, 1);
        Assert.Throws<InvalidOperationException>(() => leftSig.Zip(foreignSig));
    }

#if !RANVIER_TRACE
    [Fact]
    public void SignalZipTwoArgAllocatesZeroBytesOnSteadyStateUpdates()
    {
        using var active = new Graph().Activate();
        var a = Signal(0);
        var b = Signal(1);
        var zipped = a.Zip(b);

        for (var i = 0; i < 2_000; i++)
        {
            a.Value = i;
            _ = zipped.Value;
        }

        var sum = 0L;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 2_000; i < 3_000; i++)
        {
            a.Value = i;
            var (x, y) = zipped.Value;
            sum += x + y;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0);
    }
#endif

    [Fact]
    public void LinqQueryComprehensionUpdatesInnerWithoutReRunningOuterBinderAndAppliesResultCutoff()
    {
        using var active = new Graph().Activate();
        var sig = Signal(2);
        var offset = Signal(3);
        var outerBinderRuns = 0;
        var outerCleanups = 0;
        var effectRuns = 0;

        var query =
            from x in sig
            from y in TrackOuterCreation(x, offset, () => outerBinderRuns++, () => outerCleanups++)
            select x * y;

        Effect(() =>
        {
            _ = query.Value;
            effectRuns++;
        });
        Flush();

        Assert.Equal(10, query.Value); // 2 * (2 + 3)
        Assert.Equal(1, outerBinderRuns);
        Assert.Equal(0, outerCleanups);
        Assert.Equal(1, effectRuns);

        // Inner offset tick updates query without re-running outer collectionSelector or discharging its cleanup.
        offset.Value = 5;
        Flush();
        Assert.Equal(14, query.Value); // 2 * (2 + 5)
        Assert.Equal(1, outerBinderRuns);
        Assert.Equal(0, outerCleanups);
        Assert.Equal(2, effectRuns);

        // Outer switch to 0 discharges previous outer scope and runs outer binder once more.
        sig.Value = 0;
        Flush();
        Assert.Equal(0, query.Value); // 0 * (0 + 5)
        Assert.Equal(2, outerBinderRuns);
        Assert.Equal(1, outerCleanups);
        Assert.Equal(3, effectRuns);

        // While sig == 0, inner offset ticks recompute 0 * (0 + 10) == 0; R equality cutoff keeps effect asleep!
        offset.Value = 10;
        Flush();
        Assert.Equal(0, query.Value);
        Assert.Equal(2, outerBinderRuns);
        Assert.Equal(3, effectRuns);

        // Three-from LINQ comprehension also exercises Memo.SelectMany(source, collectionSelector, resultSelector).
        var bonus = Signal(1);
        var threeStage =
            from x in sig
            from y in Memo(() => x + offset.Value)
            from z in Memo(() => y + bonus.Value)
            select x + y + z;

        sig.Value = 2;
        Flush();
        Assert.Equal(27, threeStage.Value); // x=2, y=12, z=13 => 27
        bonus.Value = 4;
        Assert.Equal(30, threeStage.Value); // x=2, y=12, z=16 => 30

        static Memo<int> TrackOuterCreation(int x, Signal<int> offset, Action onBind, Action onClean)
        {
            onBind();
            OnCleanup(onClean);
            return Memo(() => x + offset.Value);
        }
    }

    [Fact]
    public void SelectManyTwoArgAndNullOrCrossGraphInnerMemoFailNodeCleanly()
    {
        using var active = new Graph().Activate();
        var useFirst = Signal(true);
        var first = Signal(10).Select(x => x);
        var second = Signal(20).Select(x => x);

        var switchedSig = useFirst.SelectMany(flag => flag ? first : second);
        var switchedMemo = useFirst.Select(flag => flag).SelectMany(flag => flag ? first : second);

        Assert.Equal(10, switchedSig.Value);
        Assert.Equal(10, switchedMemo.Value);
        useFirst.Value = false;
        Assert.Equal(20, switchedSig.Value);
        Assert.Equal(20, switchedMemo.Value);

        // Null inner Memo returned at evaluation fails the SelectMany Memo with NullReferenceException (both 2-arg and 3-arg).
        var nullInner2 = useFirst.SelectMany(_ => (Memo<int>)null!);
        var nullInner3 = useFirst.SelectMany(_ => (Memo<int>)null!, (flag, v) => v);
        var nullMemoInner3 = first.SelectMany(_ => (Memo<int>)null!, (a, b) => a + b);
        Assert.Throws<NullReferenceException>(() => _ = nullInner2.Value);
        Assert.Throws<NullReferenceException>(() => _ = nullInner3.Value);
        Assert.Throws<NullReferenceException>(() => _ = nullMemoInner3.Value);

        // Cross-graph inner Memo returned at evaluation fails with InvalidOperationException without leaking onto or mutating foreign graph.
        using var foreignGraph = new Graph();
        var foreignSig = new Signal<int>(foreignGraph, 99);
        var foreignMemo = foreignSig.Select(x => x);
#if RANVIER_TRACE
        var foreignEventsBefore = Tracing.Events(foreignGraph).Length;
#endif
        var crossSig2 = useFirst.SelectMany(_ => foreignMemo);
        var crossSig3 = useFirst.SelectMany(_ => foreignMemo, (_, v) => v);
        var crossMemo3 = first.SelectMany(_ => foreignMemo, (_, v) => v);
        var expectedCrossGraphMsg = Assert.Throws<InvalidOperationException>(() => _ = crossSig2.Value).Message;
        Assert.Equal(expectedCrossGraphMsg, Assert.Throws<InvalidOperationException>(() => _ = crossSig3.Value).Message);
        Assert.Equal(expectedCrossGraphMsg, Assert.Throws<InvalidOperationException>(() => _ = crossMemo3.Value).Message);
#if RANVIER_TRACE
        Assert.Equal(foreignEventsBefore, Tracing.Events(foreignGraph).Length);
#endif
        // foreignMemo stays unmutated, readable, and still eligible to fuse on its own graph.
        var foreignDownstream = foreignMemo.Select(x => x + 1);
        Assert.Equal(99, foreignMemo.Value);
        Assert.Equal(100, foreignDownstream.Value);
        foreignSig.Value = 41;
        Assert.Equal(42, foreignDownstream.Value);

        // Guarded foreign graph owned by another thread still fails with crossGraphBind rather than off-thread creation error.
        using var bgReady = new ManualResetEventSlim();
        using var bgRelease = new ManualResetEventSlim();
        Memo<int>? bgMemo = null;
        var ct = TestContext.Current.CancellationToken;
        var bgThread = new Thread(() =>
        {
            using var bgGraph = new Graph();
            bgMemo = new Signal<int>(bgGraph, 7).Select(x => x * 2);
            bgReady.Set();
            bgRelease.Wait(ct);
        });
        bgThread.Start();
        bgReady.Wait(ct);

        try
        {
            var crossBgSig3 = useFirst.SelectMany(_ => bgMemo!, (_, v) => v);
            var crossBgMemo3 = first.SelectMany(_ => bgMemo!, (_, v) => v);
            Assert.Equal(expectedCrossGraphMsg, Assert.Throws<InvalidOperationException>(() => _ = crossBgSig3.Value).Message);
            Assert.Equal(expectedCrossGraphMsg, Assert.Throws<InvalidOperationException>(() => _ = crossBgMemo3.Value).Message);
        }
        finally
        {
            bgRelease.Set();
            bgThread.Join();
        }
    }

    [Fact]
    public void AsyncAndStreamSelectManyAndSelectManyFoldChainFlightsAndStreams()
    {
        var graph = new Graph(GraphOptions.Default.WithDispatcher(new ManualDispatcher()));
        using var active = graph.Activate();

        // AsyncSource.SelectMany -> AsyncMemo.SelectMany -> AsyncMemo.SelectMany(..., FlightPolicy)
        var userId = AsyncSource<int>();
        var profile = userId.SelectMany((id, _) => Task.FromResult($"user-{id}"));
        var greeting = profile.SelectMany((name, _) => Task.FromResult($"Hello, {name}!"));

        var policyGates = new List<(string Input, TaskCompletionSource<string> Reply)>();
        var withPolicy = profile.SelectMany(
            (name, _) =>
            {
                var tcs = new TaskCompletionSource<string>();
                policyGates.Add((name, tcs));
                return tcs.Task;
            },
            FlightPolicy.FinishCurrent);

        Assert.True(greeting.TryValue.IsPending);
        Assert.True(withPolicy.TryValue.IsPending);

        userId.Settle(7);
        Assert.Equal("Hello, user-7!", greeting.Value);
        Assert.True(withPolicy.TryValue.IsPending);
        Assert.Single(policyGates);

        userId.Settle(8);
        Assert.Equal("Hello, user-8!", greeting.Value);
        // FinishCurrent holds off second flight while first is still running.
        Assert.True(withPolicy.TryValue.IsPending);
        Assert.Single(policyGates);

        policyGates[0].Reply.SetResult("first");
        Assert.True(withPolicy.TryValue.IsPending);
        Assert.Equal(2, policyGates.Count);
        policyGates[1].Reply.SetResult("second:user-8");
        Assert.Equal("second:user-8", withPolicy.Value);

        // StreamMemo.SelectMany and StreamMemo.SelectManyFold
        StreamSink<string>? rooms = null;
        var roomStream = Stream(_ => StreamSources.Create<string>((s, _) =>
        {
            rooms = s;
            return new TaskCompletionSource().Task;
        }));

        var childStreams = new Dictionary<string, StreamSink<int>>();
        var latestMsg = roomStream.SelectMany((room, _) =>
            StreamSources.Create<int>((s, _) =>
            {
                childStreams[room + ":latest"] = s;
                return new TaskCompletionSource().Task;
            }));

        var totalMsgs = roomStream.SelectManyFold(
            0,
            (acc, n) => acc + n,
            (room, _) =>
                StreamSources.Create<int>((s, _) =>
                {
                    childStreams[room + ":fold"] = s;
                    return new TaskCompletionSource().Task;
                }));

        Assert.True(latestMsg.TryValue.IsPending);
        Assert.True(totalMsgs.TryValue.IsPending);

        rooms!.Next("general");
        Assert.True(latestMsg.TryValue.IsPending);
        Assert.True(totalMsgs.TryValue.IsPending);

        childStreams["general:latest"].Next(11);
        childStreams["general:fold"].Next(4);
        childStreams["general:fold"].Next(6);
        Assert.Equal(11, latestMsg.Value);
        Assert.Equal(10, totalMsgs.Value);

        rooms.Next("random");
        Assert.True(latestMsg.TryValue.IsPending);
        Assert.True(totalMsgs.TryValue.IsPending);
        childStreams["random:latest"].Next(99);
        childStreams["random:fold"].Next(5);
        Assert.Equal(99, latestMsg.Value);
        Assert.Equal(5, totalMsgs.Value);
    }

    [Fact]
    public void EagerArgumentNullChecksRejectNullDelegatesAndSourcesBeforeEvaluation()
    {
        using var active = new Graph().Activate();
        var sig = Signal(1);
        var memo = sig.Select(x => x);
        var asyncSrc = AsyncSource<int>();
        var asyncMemo = Async(_ => Task.FromResult(1));
        var streamMemo = Stream(_ => StreamSources.Create<int>((_, _) => new TaskCompletionSource().Task));

        // Reactive.*On null guards
        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => MemoOn<int, int>(null!, x => x)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => MemoOn<int, int>(() => 1, null!)).ParamName);
        Assert.Equal("comparer", Assert.Throws<ArgumentNullException>(() => MemoOn(() => 1, x => x, null!)).ParamName);

        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => OwningMemoOn<int, int>(null!, x => x)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => OwningMemoOn<int, int>(() => 1, null!)).ParamName);
        Assert.Equal("comparer", Assert.Throws<ArgumentNullException>(() => OwningMemoOn(() => 1, x => x, null!)).ParamName);

        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => AsyncOn<int, int>(null!, (_, _) => Task.FromResult(1))).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => AsyncOn(() => 1, (Func<int, CancellationToken, Task<int>>)null!)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => AsyncOn(() => 1, (Func<int, Previous<int>, CancellationToken, Task<int>>)null!)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => AsyncOn(() => 1, (Func<int, CancellationToken, Task<int>>)null!, FlightPolicy.KeepLatest)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => AsyncOn(() => 1, (Func<int, Previous<int>, CancellationToken, Task<int>>)null!, FlightPolicy.KeepLatest)).ParamName);

        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => OwningAsyncOn<int, int>(null!, (_, _) => Task.FromResult(1))).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => OwningAsyncOn(() => 1, (Func<int, CancellationToken, Task<int>>)null!)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => OwningAsyncOn(() => 1, (Func<int, Previous<int>, CancellationToken, Task<int>>)null!)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => OwningAsyncOn(() => 1, (Func<int, CancellationToken, Task<int>>)null!, FlightPolicy.KeepLatest)).ParamName);
        Assert.Equal("compute", Assert.Throws<ArgumentNullException>(() => OwningAsyncOn(() => 1, (Func<int, Previous<int>, CancellationToken, Task<int>>)null!, FlightPolicy.KeepLatest)).ParamName);

        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => StreamOn<int, int>(null!, (_, _) => null!)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentNullException>(() => StreamOn<int, int>(() => 1, null!)).ParamName);

        Assert.Equal("fold", Assert.Throws<ArgumentNullException>(() => StreamFoldOn<int, int, int>(0, null!, () => 1, (_, _) => null!)).ParamName);
        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => StreamFoldOn<int, int, int>(0, (s, x) => s + x, null!, (_, _) => null!)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentNullException>(() => StreamFoldOn<int, int, int>(0, (s, x) => s + x, () => 1, null!)).ParamName);

        // RanvierExtensions.Select null guards
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((Signal<int>)null!).Select(x => x)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => sig.Select<int, int>(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((Memo<int>)null!).Select(x => x)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => memo.Select<int, int>(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((AsyncMemo<int>)null!).Select(x => x)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => asyncMemo.Select<int, int>(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((AsyncSource<int>)null!).Select(x => x)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => asyncSrc.Select<int, int>(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((StreamMemo<int>)null!).Select(x => x)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => streamMemo.Select<int, int>(null!)).ParamName);

        // RanvierExtensions.Zip null guards (2-arg and 3-arg)
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => ((Signal<int>)null!).Zip(sig)).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => sig.Zip((Signal<int>)null!)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => sig.Zip<int, int, int>(sig, null!)).ParamName);
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => ((Memo<int>)null!).Zip(memo)).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => memo.Zip((Memo<int>)null!)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => memo.Zip<int, int, int>(memo, null!)).ParamName);
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => ((AsyncMemo<int>)null!).Zip(asyncMemo)).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => asyncMemo.Zip((AsyncMemo<int>)null!)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => asyncMemo.Zip<int, int, int>(asyncMemo, null!)).ParamName);
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => ((AsyncSource<int>)null!).Zip(asyncSrc)).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => asyncSrc.Zip((AsyncSource<int>)null!)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => asyncSrc.Zip<int, int, int>(asyncSrc, null!)).ParamName);
        Assert.Equal("first", Assert.Throws<ArgumentNullException>(() => ((StreamMemo<int>)null!).Zip(streamMemo)).ParamName);
        Assert.Equal("second", Assert.Throws<ArgumentNullException>(() => streamMemo.Zip((StreamMemo<int>)null!)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => streamMemo.Zip<int, int, int>(streamMemo, null!)).ParamName);

        // RanvierExtensions.SelectMany & SelectManyFold null guards
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((Signal<int>)null!).SelectMany(_ => memo)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => sig.SelectMany((Func<int, Memo<int>>)null!)).ParamName);
        Assert.Equal("collectionSelector", Assert.Throws<ArgumentNullException>(() => sig.SelectMany<int, int, int>(null!, (a, b) => a + b)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => sig.SelectMany(_ => memo, (Func<int, int, int>)null!)).ParamName);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((Memo<int>)null!).SelectMany(_ => memo)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => memo.SelectMany((Func<int, Memo<int>>)null!)).ParamName);
        Assert.Equal("collectionSelector", Assert.Throws<ArgumentNullException>(() => memo.SelectMany<int, int, int>(null!, (a, b) => a + b)).ParamName);
        Assert.Equal("resultSelector", Assert.Throws<ArgumentNullException>(() => memo.SelectMany(_ => memo, (Func<int, int, int>)null!)).ParamName);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((AsyncMemo<int>)null!).SelectMany((_, _) => Task.FromResult(1))).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => asyncMemo.SelectMany<int, int>(null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => asyncMemo.SelectMany<int, int>(null!, FlightPolicy.KeepLatest)).ParamName);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((AsyncSource<int>)null!).SelectMany((_, _) => Task.FromResult(1))).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => asyncSrc.SelectMany<int, int>(null!)).ParamName);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((StreamMemo<int>)null!).SelectMany((_, _) => StreamSources.Create<int>((_, _) => Task.CompletedTask))).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => streamMemo.SelectMany<int, int>(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((StreamMemo<int>)null!).SelectManyFold(0, (s, x) => s + x, (_, _) => StreamSources.Create<int>((_, _) => Task.CompletedTask))).ParamName);
        Assert.Equal("fold", Assert.Throws<ArgumentNullException>(() => streamMemo.SelectManyFold<int, int, int>(0, null!, (_, _) => StreamSources.Create<int>((_, _) => Task.CompletedTask))).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentNullException>(() => streamMemo.SelectManyFold<int, int, int>(0, (s, x) => s + x, null!)).ParamName);
    }
}
