using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

// Provides raw command execution for a tmux session.
public sealed partial class Session
{
    private readonly TmuxCommandDispatcher _commandDispatcher;
    private readonly string _defaultTarget;

    internal Session(TmuxCommandDispatcher commandDispatcher, string defaultTarget)
    {
        _commandDispatcher = commandDispatcher
            ?? throw new ArgumentNullException(nameof(commandDispatcher));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTarget);
        _defaultTarget = defaultTarget;
    }

    /// <summary>Executes one raw tmux command against this session.</summary>
    [UnsupportedOSPlatform("windows")]
    public Task<TmuxCommandResult> ExecuteCommandAsync(
        IReadOnlyList<string> arguments,
        string? targetOverride = null,
        CancellationToken cancellationToken = default)
    {
        return TargetedCommandArguments.ExecuteAsync(
            _commandDispatcher,
            arguments,
            targetOverride ?? _defaultTarget,
            cancellationToken);
    }
}

internal static class TargetedCommandArguments
{
    [UnsupportedOSPlatform("windows")]
    internal static Task<TmuxCommandResult> ExecuteAsync(
        TmuxCommandDispatcher dispatcher,
        IReadOnlyList<string> arguments,
        string target,
        CancellationToken cancellationToken)
    {
        TmuxCommandDispatcher.ValidateArguments(arguments);
        RejectRawTargetOptions(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        var targeted = new string[arguments.Count + 2];
        targeted[0] = arguments[0];
        targeted[1] = "-t";
        targeted[2] = target;
        for (int index = 1; index < arguments.Count; index++)
        {
            targeted[index + 2] = arguments[index];
        }

        return dispatcher.ExecuteAsync(targeted, cancellationToken);
    }

    private static void RejectRawTargetOptions(IReadOnlyList<string> arguments)
    {
        for (int index = 1; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (argument == "--")
            {
                return;
            }

            if (ContainsTargetOption(argument))
            {
                throw new ArgumentException(
                    "Raw target options are not allowed; use targetOverride instead.",
                    nameof(arguments));
            }
        }
    }

    private static bool ContainsTargetOption(string argument)
    {
        if (argument.Length < 2 || argument[0] != '-')
        {
            return false;
        }

        for (int index = 1; index < argument.Length; index++)
        {
            char option = argument[index];
            if (!char.IsAsciiLetterOrDigit(option))
            {
                return false;
            }

            if (option == 't')
            {
                return true;
            }
        }

        return false;
    }
}
