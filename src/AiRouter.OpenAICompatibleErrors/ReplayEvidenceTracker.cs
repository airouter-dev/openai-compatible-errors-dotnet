using System.Threading;

namespace AiRouter.OpenAICompatibleErrors;

/// <summary>
/// Records only monotonic replay-boundary evidence and never retains response text or tool arguments.
/// </summary>
public sealed class ReplayEvidenceTracker
{
    private int progress;

    /// <summary>Gets an atomic snapshot of the strongest evidence observed so far.</summary>
    public StreamProgress Snapshot => (StreamProgress)Volatile.Read(ref progress);

    /// <summary>Records that response-body bytes arrived without claiming they were semantic output.</summary>
    public void ObserveTransportBytes(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must not be negative.");
        }

        if (byteCount > 0)
        {
            Advance(StreamProgress.TransportBytes);
        }
    }

    /// <summary>
    /// Records non-empty user-visible text. The supplied value is inspected for emptiness and is not retained.
    /// </summary>
    public void ObserveText(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Advance(StreamProgress.SemanticOutput);
        }
    }

    /// <summary>Records a non-empty tool-call fragment by length, without accepting or retaining its contents.</summary>
    public void ObserveToolCallFragment(int characterCount)
    {
        if (characterCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCount), "Character count must not be negative.");
        }

        if (characterCount > 0)
        {
            Advance(StreamProgress.SemanticOutput);
        }
    }

    /// <summary>Records an unclassifiable stream event or parser state.</summary>
    public void MarkUncertain()
    {
        Advance(StreamProgress.Uncertain);
    }

    /// <summary>Records a success, failure, incomplete, or other terminal event.</summary>
    public void ObserveTerminal()
    {
        Advance(StreamProgress.Terminal);
    }

    private void Advance(StreamProgress candidate)
    {
        var candidateValue = (int)candidate;
        while (true)
        {
            var current = Volatile.Read(ref progress);
            if (current >= candidateValue)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref progress, candidateValue, current) == current)
            {
                return;
            }
        }
    }
}
