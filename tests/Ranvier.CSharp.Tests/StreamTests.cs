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
        using var active = new Graph().Activate();
        var memo = Reactive.StreamFold(0, (int s, int x) => s + x, _ => StreamSources.FromAsyncEnumerable(Items()));

        var done = false;
        for (var i = 0; i < 100 && !done; i++)
        {
            Thread.Sleep(50);
            done = memo.TryValue.TryGetValue(out var total) && total == 6;
        }

        Assert.True(done);
    }

    private static async IAsyncEnumerable<int> Items()
    {
        await Task.Yield();
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
