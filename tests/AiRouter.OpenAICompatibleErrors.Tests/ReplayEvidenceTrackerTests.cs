namespace AiRouter.OpenAICompatibleErrors.Tests;

public sealed class ReplayEvidenceTrackerTests
{
    [Fact]
    public void EmptyObservationsDoNotAdvanceBoundary()
    {
        var tracker = new ReplayEvidenceTracker();

        tracker.ObserveTransportBytes(0);
        tracker.ObserveText(null);
        tracker.ObserveText(string.Empty);
        tracker.ObserveToolCallFragment(0);

        Assert.Equal(StreamProgress.None, tracker.Snapshot);
    }

    [Fact]
    public void EvidenceOnlyMovesTowardMoreRestrictiveStates()
    {
        var tracker = new ReplayEvidenceTracker();

        tracker.ObserveTransportBytes(1);
        Assert.Equal(StreamProgress.TransportBytes, tracker.Snapshot);

        tracker.MarkUncertain();
        Assert.Equal(StreamProgress.Uncertain, tracker.Snapshot);

        tracker.ObserveText("not retained");
        Assert.Equal(StreamProgress.SemanticOutput, tracker.Snapshot);

        tracker.ObserveTransportBytes(10);
        Assert.Equal(StreamProgress.SemanticOutput, tracker.Snapshot);

        tracker.ObserveTerminal();
        Assert.Equal(StreamProgress.Terminal, tracker.Snapshot);

        tracker.ObserveText("cannot move backward");
        Assert.Equal(StreamProgress.Terminal, tracker.Snapshot);
    }

    [Fact]
    public void ToolFragmentLengthCommitsSemanticOutputWithoutContent()
    {
        var tracker = new ReplayEvidenceTracker();

        tracker.ObserveToolCallFragment(17);

        Assert.Equal(StreamProgress.SemanticOutput, tracker.Snapshot);
    }

    [Fact]
    public void ConcurrentObservationsProduceStrongestEvidence()
    {
        var tracker = new ReplayEvidenceTracker();
        var actions = new Action[]
        {
            () => tracker.ObserveTransportBytes(1),
            tracker.MarkUncertain,
            () => tracker.ObserveText("text"),
            tracker.ObserveTerminal,
        };

        Parallel.For(0, 10_000, index => actions[index % actions.Length]());

        Assert.Equal(StreamProgress.Terminal, tracker.Snapshot);
    }

    [Fact]
    public void NegativeLengthsAreRejected()
    {
        var tracker = new ReplayEvidenceTracker();

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.ObserveTransportBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.ObserveToolCallFragment(-1));
    }
}
