namespace LibTmux.UnitTests.Exceptions;

/// <summary>Proves the failure a caller catches says whether retrying is safe.</summary>
/// <remarks>
/// Retrying a failed tmux command is the obvious recovery and it is only sound
/// when the command never ran. These tests pin which failures may claim that,
/// because the cost of the claim being wrong is a <c>kill-session</c> or a
/// <c>send-keys</c> happening twice.
/// </remarks>
public sealed class DispatchStateTests
{
    [Fact]
    public void A_failure_says_nothing_about_dispatch_unless_it_knows()
    {
        // The parameterless-dispatch constructor is what most failures use, and
        // defaulting it to NotDispatched would invite exactly the unsafe retry
        // this type exists to prevent.
        LibTmuxException failure = new("something went wrong");

        Assert.Equal(TmuxDispatchState.Unknown, failure.Dispatch);
    }

    [Fact]
    public void A_missing_tmux_binary_never_ran_anything()
    {
        TmuxCommandNotFoundException failure = new("not found", "/nonexistent/tmux");

        Assert.Equal(TmuxDispatchState.NotDispatched, failure.Dispatch);
    }

    [Fact]
    public void A_command_tmux_answered_has_already_had_its_effect()
    {
        TmuxCommandResult result = new(
            arguments: ["kill-session", "-t", "build"],
            exitCode: 1,
            standardOutput: ReadOnlyMemory<byte>.Empty,
            standardError: ReadOnlyMemory<byte>.Empty,
            standardOutputLines: [],
            standardErrorLines: ["can't find session: build"]);

        TmuxCommandException failure = new("kill-session failed.", result);

        // The exception exists because tmux ran and answered, so this is a fact
        // about the type rather than something a caller can pass in.
        Assert.Equal(TmuxDispatchState.Dispatched, failure.Dispatch);
    }

    [Fact]
    public void A_transport_failure_defaults_to_unknown()
    {
        TmuxTransportException failure = new("the pipe broke", ["list-sessions"]);

        Assert.Equal(TmuxDispatchState.Unknown, failure.Dispatch);
    }

    [Fact]
    public void A_transport_failure_can_say_tmux_was_never_started()
    {
        TmuxTransportException failure = new(
            "The tmux client process could not be started.",
            ["list-sessions"],
            TmuxDispatchState.NotDispatched);

        Assert.Equal(TmuxDispatchState.NotDispatched, failure.Dispatch);
    }

    [Fact]
    public void The_retry_decision_reads_as_an_exception_filter()
    {
        // This is the shape the documentation promises, so it is worth proving
        // it compiles and selects rather than only describing it.
        static bool SafeToRetry(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (LibTmuxException error) when (error.Dispatch == TmuxDispatchState.NotDispatched)
            {
                return true;
            }
            catch (LibTmuxException)
            {
                return false;
            }
        }

        Assert.True(SafeToRetry(
            () => throw new TmuxCommandNotFoundException("not found", "/nonexistent/tmux")));
        Assert.False(SafeToRetry(
            () => throw new TmuxTransportException("the pipe broke", ["list-sessions"])));
    }
}
