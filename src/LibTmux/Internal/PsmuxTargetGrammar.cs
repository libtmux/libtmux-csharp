namespace LibTmux.Internal;

internal static class PsmuxTargetGrammar
{
    internal static void ValidateName(string name, string kind)
    {
        PsmuxCommandPolicy.ValidateArgument(name);
        if (name.Contains("__", StringComparison.Ordinal)
            || name.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new NotSupportedException(
                $"psmux {kind} names must use only ASCII letters, digits, '-' or '_' and cannot contain '__'.");
        }
    }

    internal static void ValidateTarget(string target)
    {
        string unmarked = target.StartsWith('=') ? target[1..] : target;
        if (WindowId.TryParse(unmarked, out _)
            || PaneId.TryParse(unmarked, out _))
        {
            return;
        }

        int separator = unmarked.IndexOf(':');
        string selector = separator < 0 ? unmarked : unmarked[..separator];
        if (selector.Length > 0 && !SessionId.TryParse(selector, out _))
        {
            ValidateName(selector, "target session");
        }

        if (separator < 0)
        {
            if (selector.Length == 0)
            {
                throw new NotSupportedException("psmux query targets cannot be empty.");
            }

            return;
        }

        string objectTarget = unmarked[(separator + 1)..];
        if (!WindowId.TryParse(objectTarget, out _)
            && !PaneId.TryParse(objectTarget, out _))
        {
            throw new NotSupportedException(
                "The psmux query preview accepts canonical session, window, and pane targets only.");
        }
    }

    internal static List<string> RewriteSessionTarget(
        IReadOnlyList<string> arguments,
        PsmuxSessionState session)
    {
        string command = arguments[0];
        var rewritten = new List<string>(arguments);
        if (command == "list-sessions")
        {
            return rewritten;
        }

        int allIndex = FindFlagIndex(rewritten, "-a");
        if ((command is "list-windows" or "list-panes")
            && allIndex >= 0)
        {
            rewritten.RemoveAt(allIndex);
            if (command == "list-panes"
                && FindFlagIndex(rewritten, "-s") < 0)
            {
                rewritten.Insert(1, "-s");
            }
        }

        int targetIndex = FindOptionOperand(rewritten, "-t");
        if (targetIndex < 0)
        {
            rewritten.InsertRange(1, ["-t", session.Name]);
        }
        else
        {
            rewritten[targetIndex] = BindTarget(rewritten[targetIndex], session);
        }

        return rewritten;
    }

    internal static int FindOptionOperand(
        IReadOnlyList<string> arguments,
        string option)
    {
        for (int index = 1; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], "--", StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(arguments[index], option, StringComparison.Ordinal))
            {
                return index + 1 < arguments.Count ? index + 1 : -1;
            }

            if (OptionTakesValue(arguments[0], arguments[index]))
            {
                index++;
            }
        }

        return -1;
    }

    private static string BindTarget(string target, PsmuxSessionState session)
    {
        bool exact = target.StartsWith('=');
        string unmarked = exact ? target[1..] : target;
        if (unmarked.Length == 0)
        {
            throw new NotSupportedException("psmux query targets cannot be empty.");
        }

        string replacement;
        if (unmarked.StartsWith(':'))
        {
            replacement = $"{session.Name}:{EncodeObjectTarget(unmarked[1..])}";
        }
        else if (WindowId.TryParse(unmarked, out _))
        {
            replacement = $"{session.Name}:{unmarked}";
        }
        else if (PaneId.TryParse(unmarked, out _))
        {
            replacement = $"{session.Name}:.{unmarked}";
        }
        else
        {
            int separator = unmarked.IndexOf(':');
            string selector = separator < 0 ? unmarked : unmarked[..separator];
            if (!SessionSelectorMatches(selector, session))
            {
                throw new NotSupportedException(
                    "The psmux query target does not match the sole visible session.");
            }

            replacement = separator < 0
                ? session.Name
                : $"{session.Name}:{EncodeObjectTarget(unmarked[(separator + 1)..])}";
        }

        return exact ? $"={replacement}" : replacement;
    }

    private static string EncodeObjectTarget(string target) =>
        PaneId.TryParse(target, out _) ? $".{target}" : target;

    private static bool SessionSelectorMatches(
        string selector,
        PsmuxSessionState session) =>
        SessionId.TryParse(selector, out SessionId id)
            ? id == session.Id
            : string.Equals(selector, session.Name, StringComparison.Ordinal);

    private static int FindFlagIndex(List<string> arguments, string flag)
    {
        for (int index = 1; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], flag, StringComparison.Ordinal))
            {
                return index;
            }

            if (OptionTakesValue(arguments[0], arguments[index]))
            {
                index++;
            }
        }

        return -1;
    }

    private static bool OptionTakesValue(string command, string option) =>
        command switch
        {
            "has-session" => option == "-t",
            "list-sessions" => option == "-F",
            "list-windows" or "list-panes" => option is "-t" or "-F",
            "display-message" => option is "-t" or "-d",
            "capture-pane" => option is "-t" or "-S" or "-E",
            _ => false,
        };
}
