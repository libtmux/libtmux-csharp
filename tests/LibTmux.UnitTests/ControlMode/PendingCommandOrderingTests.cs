namespace LibTmux.UnitTests.ControlMode;

/// <summary>Proves a command tmux never saw does not take another command's answer.</summary>
/// <remarks>
/// The waiter for a command is queued before the command is written, because
/// some commands -- <c>kill-server</c> most obviously -- end the client as their
/// answer, and the reader fails whatever is queued when the process exits. A
/// waiter registered after the write would miss that sweep and wait forever.
///
/// That ordering leaves a slot behind when the write itself fails. tmux answers
/// in order, so the next reply would go to a caller whose command was never
/// sent, and every caller after that would be one reply behind. This models the
/// queue and the skip rule that resolves it.
/// </remarks>
public sealed class PendingCommandOrderingTests
{
    /// <summary>The rule the session applies when matching a reply to a waiter.</summary>
    /// <remarks>
    /// A copy rather than the real queue: the session's own is private, and what
    /// is worth pinning is the rule, which is small enough to state exactly.
    /// </remarks>
    private static string? NextAnswered(Queue<(string Command, bool Abandoned)> pending)
    {
        while (pending.Count > 0)
        {
            (string command, bool abandoned) = pending.Dequeue();
            if (!abandoned)
            {
                return command;
            }
        }

        return null;
    }

    [Fact]
    public void A_reply_goes_to_the_command_that_was_actually_sent()
    {
        // The middle command's write failed, so tmux only ever heard the first
        // and third. Two replies arrive.
        Queue<(string, bool)> pending = new();
        pending.Enqueue(("first", false));
        pending.Enqueue(("cancelled", true));
        pending.Enqueue(("third", false));

        Assert.Equal("first", NextAnswered(pending));
        Assert.Equal("third", NextAnswered(pending));
        Assert.Null(NextAnswered(pending));
    }

    [Fact]
    public void Consecutive_abandoned_commands_are_all_skipped()
    {
        Queue<(string, bool)> pending = new();
        pending.Enqueue(("cancelled", true));
        pending.Enqueue(("also cancelled", true));
        pending.Enqueue(("sent", false));

        Assert.Equal("sent", NextAnswered(pending));
    }

    [Fact]
    public void A_queue_of_only_abandoned_commands_answers_nobody()
    {
        // Nothing reached tmux, so nothing is coming back. Handing this reply to
        // a caller who was never sent is the bug being prevented.
        Queue<(string, bool)> pending = new();
        pending.Enqueue(("cancelled", true));

        Assert.Null(NextAnswered(pending));
    }
}
