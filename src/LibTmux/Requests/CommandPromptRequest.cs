namespace LibTmux;

/// <summary>What a command prompt is asking for.</summary>
/// <remarks>
/// tmux uses the type to decide which history the prompt draws on and how it
/// completes what is typed.
/// </remarks>
public enum PromptType
{
    /// <summary>A tmux command.</summary>
    Command,

    /// <summary>Text to search for.</summary>
    Search,

    /// <summary>A target to act on.</summary>
    Target,

    /// <summary>A window to act on.</summary>
    WindowTarget,
}

/// <summary>Describes one <c>command-prompt</c> invocation.</summary>
public sealed record CommandPromptRequest
{
    /// <summary>Initializes a command prompt.</summary>
    /// <param name="template">The command to run, with the answer substituted in.</param>
    /// <param name="prompt">The text shown to the person answering.</param>
    /// <param name="inputs">The answer the prompt starts with.</param>
    /// <param name="targetClient">The client to prompt, or null for the caller's own.</param>
    /// <param name="oneKey">Whether one keypress answers it.</param>
    /// <param name="keyOnly">Whether the answer is the key itself rather than text.</param>
    /// <param name="onInputChange">Whether the command runs on every keystroke.</param>
    /// <param name="numeric">Whether only digits are accepted.</param>
    /// <param name="type">What the prompt is asking for.</param>
    /// <param name="expandFormat">Whether the template is expanded as a format.</param>
    /// <param name="literal">Whether the answer is taken literally.</param>
    /// <param name="backspaceExits">Whether backspace on an empty prompt closes it.</param>
    /// <param name="noFreeze">Whether the client keeps redrawing while prompting.</param>
    public CommandPromptRequest(
        string template,
        string? prompt = null,
        string? inputs = null,
        string? targetClient = null,
        bool oneKey = false,
        bool keyOnly = false,
        bool onInputChange = false,
        bool numeric = false,
        PromptType? type = null,
        bool expandFormat = false,
        bool literal = false,
        bool backspaceExits = false,
        bool noFreeze = false)
    {
        ArgumentNullException.ThrowIfNull(template);
        Template = template;
        Prompt = prompt;
        Inputs = inputs;
        TargetClient = targetClient;
        OneKey = oneKey;
        KeyOnly = keyOnly;
        OnInputChange = onInputChange;
        Numeric = numeric;
        Type = type;
        ExpandFormat = expandFormat;
        Literal = literal;
        BackspaceExits = backspaceExits;
        NoFreeze = noFreeze;
    }

    /// <summary>Gets the command to run, with the answer substituted in.</summary>
    public string Template { get; }

    /// <summary>Gets the text shown to the person answering.</summary>
    public string? Prompt { get; }

    /// <summary>Gets the answer the prompt starts with.</summary>
    public string? Inputs { get; }

    /// <summary>Gets the client to prompt, or null for the caller's own.</summary>
    public string? TargetClient { get; }

    /// <summary>Gets whether one keypress answers it.</summary>
    public bool OneKey { get; }

    /// <summary>Gets whether the answer is the key itself rather than text.</summary>
    public bool KeyOnly { get; }

    /// <summary>Gets whether the command runs on every keystroke.</summary>
    public bool OnInputChange { get; }

    /// <summary>Gets whether only digits are accepted.</summary>
    public bool Numeric { get; }

    /// <summary>Gets what the prompt is asking for.</summary>
    public PromptType? Type { get; }

    /// <summary>Gets whether the template is expanded as a format.</summary>
    public bool ExpandFormat { get; }

    /// <summary>Gets whether the answer is taken literally.</summary>
    public bool Literal { get; }

    /// <summary>Gets whether backspace on an empty prompt closes it.</summary>
    public bool BackspaceExits { get; }

    /// <summary>Gets whether the client keeps redrawing while prompting.</summary>
    public bool NoFreeze { get; }
}
