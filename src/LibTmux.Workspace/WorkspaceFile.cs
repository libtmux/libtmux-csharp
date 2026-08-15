using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LibTmux.Workspace;

/// <summary>One pane in a tmuxp workspace file.</summary>
/// <remarks>
/// tmuxp lets a pane be written as a bare string, which means the command to
/// run, or as a mapping when it needs more than that. Both arrive here as this.
/// </remarks>
public sealed class WorkspacePane
{
    /// <summary>Gets or sets the shell command the pane starts with.</summary>
    [YamlMember(Alias = "shell_command")]
    public string? ShellCommand { get; set; }

    /// <summary>Gets or sets the directory the pane starts in.</summary>
    [YamlMember(Alias = "start_directory")]
    public string? StartDirectory { get; set; }

    /// <summary>Gets or sets whether this pane is the one left selected.</summary>
    [YamlMember(Alias = "focus")]
    public bool Focus { get; set; }
}

/// <summary>One window in a tmuxp workspace file.</summary>
public sealed class WorkspaceWindow
{
    /// <summary>Gets or sets the window name.</summary>
    [YamlMember(Alias = "window_name")]
    public string? WindowName { get; set; }

    /// <summary>Gets or sets the directory the window's panes start in.</summary>
    [YamlMember(Alias = "start_directory")]
    public string? StartDirectory { get; set; }

    /// <summary>Gets or sets the layout tmux arranges the panes with.</summary>
    [YamlMember(Alias = "layout")]
    public string? Layout { get; set; }

    /// <summary>Gets or sets whether this window is the one left selected.</summary>
    [YamlMember(Alias = "focus")]
    public bool Focus { get; set; }

    /// <summary>Gets or sets the window options set once the panes exist.</summary>
    [YamlMember(Alias = "options")]
    public Dictionary<string, string> Options { get; set; } = [];

    /// <summary>Gets or sets the panes, in the order they are created.</summary>
    [YamlMember(Alias = "panes")]
    public List<WorkspacePane> Panes { get; set; } = [];
}

/// <summary>A tmuxp workspace file.</summary>
/// <remarks>
/// Only what shapes a session is read: the name, where things start, the
/// windows, and the options. tmuxp's plugin and hook machinery runs Python and
/// has no meaning here, so a file using it still builds and what cannot be
/// honoured is reported rather than ignored.
/// </remarks>
public sealed class WorkspaceFile
{
    /// <summary>Gets or sets the session name.</summary>
    [YamlMember(Alias = "session_name")]
    public string? SessionName { get; set; }

    /// <summary>Gets or sets the directory every window starts in.</summary>
    [YamlMember(Alias = "start_directory")]
    public string? StartDirectory { get; set; }

    /// <summary>Gets or sets the session options set once the session exists.</summary>
    [YamlMember(Alias = "options")]
    public Dictionary<string, string> Options { get; set; } = [];

    /// <summary>Gets or sets the windows, in the order they are created.</summary>
    [YamlMember(Alias = "windows")]
    public List<WorkspaceWindow> Windows { get; set; } = [];

    /// <summary>Reads a workspace from tmuxp YAML.</summary>
    /// <param name="yaml">The file's contents.</param>
    /// <returns>The workspace.</returns>
    /// <exception cref="WorkspaceFormatException">The text is not a workspace.</exception>
    public static WorkspaceFile Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        IDeserializer reader = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        try
        {
            // tmuxp writes a pane as a bare string when the command is all it
            // needs, so the shape is normalised before it is bound.
            return reader.Deserialize<WorkspaceFile>(Normalize(yaml))
                ?? throw new WorkspaceFormatException("The workspace file is empty.");
        }
        catch (YamlDotNet.Core.YamlException failure)
        {
            throw new WorkspaceFormatException(
                $"The workspace file could not be read: {failure.Message}",
                failure);
        }
    }

    private static string Normalize(string yaml)
    {
        // A pane written as "- vim" means a pane running vim. Rewriting it to
        // the mapping form is what lets one reader handle both spellings.
        string[] lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        List<string> rewritten = new(lines.Length);
        bool inPanes = false;
        int panesIndent = 0;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            if (trimmed.StartsWith("panes:", StringComparison.Ordinal))
            {
                inPanes = true;
                panesIndent = indent;
                rewritten.Add(line);
                continue;
            }

            if (inPanes && trimmed.Length > 0 && indent <= panesIndent
                && !trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                inPanes = false;
            }

            if (inPanes
                && trimmed.StartsWith("- ", StringComparison.Ordinal)
                && !trimmed.Contains(": ", StringComparison.Ordinal)
                && !trimmed.EndsWith(':'))
            {
                string command = trimmed[2..].Trim();
                rewritten.Add($"{line[..indent]}- shell_command: {command}");
                continue;
            }

            rewritten.Add(line);
        }

        return string.Join('\n', rewritten);
    }
}

/// <summary>Thrown when a workspace file cannot be read.</summary>
public sealed class WorkspaceFormatException : LibTmuxException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">What is wrong with the file.</param>
    /// <param name="innerException">The underlying failure, when any.</param>
    public WorkspaceFormatException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
