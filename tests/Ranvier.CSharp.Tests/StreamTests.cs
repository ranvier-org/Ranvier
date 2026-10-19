using Ranvier;
using Ranvier.CSharp;
using Xunit;

namespace CSharpCallers;

public class StreamTests
{
    [Fact]
    public void StreamFoldPublishesFoldedItems()
    {
        using var active = new Graph().Activate();
        StreamSink<int>? sink = null;
        var memo = Reactive.StreamFold(0, (int s, int x) => s + x,
            _ => StreamSources.Create<int>((s, _) => { sink = s; return new TaskCompletionSource().Task; }));

        Assert.True(memo.TryValue.IsPending);
        sink!.Next(2);
        sink.Next(3);
        Assert.Equal(5, memo.Value);
    }

    [Fact]
    public void StreamPublishesTheLatestItem()
    {
        using var active = new Graph().Activate();
        StreamSink<string>? sink = null;
        var memo = Reactive.Stream(
            _ => StreamSources.Create<string>((s, _) => { sink = s; return new TaskCompletionSource().Task; }));

        Assert.True(memo.TryValue.IsPending);
        sink!.Next("a");
        sink.Next("b");
        Assert.Equal("b", memo.Value);
    }

    [Fact]
    public void FromAsyncEnumerableDeliversItems()
    {
        var graph = new Graph(GraphOptions.Default.WithDispatcher(new ManualDispatcher()));
        using var active = graph.Activate();
        var finished = new TaskCompletionSource();
        var memo = Reactive.StreamFold(0, (int s, int x) => s + x,
            _ => StreamSources.FromAsyncEnumerable(Items(finished)));

        Assert.True(memo.TryValue.IsPending || memo.TryValue.TryGetValue(out _));
        Assert.True(finished.Task.Wait(TimeSpan.FromSeconds(10)), "the enumerator did not finish within 10 s");
        graph.Pump();
        Assert.Equal(6, memo.Value);
    }

    private static async IAsyncEnumerable<int> Items(TaskCompletionSource finished)
    {
        try
        {
            await Task.Yield();
            yield return 1;
            yield return 2;
            yield return 3;
        }
        finally
        {
            finished.TrySetResult();
        }
    }
}
