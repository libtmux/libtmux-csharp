namespace LibTmux;

/// <summary>Describes one <c>server-access</c> invocation.</summary>
/// <remarks>
/// tmux lets other users attach to a server over its socket. Access is granted
/// per user, and read-only or read-write says what a granted user may do.
/// </remarks>
public sealed record ServerAccessRequest
{
    /// <summary>Initializes an access change.</summary>
    /// <param name="allowUser">The user to grant access to.</param>
    /// <param name="denyUser">The user to take access from.</param>
    /// <param name="list">Whether the current list is reported.</param>
    /// <param name="readOnly">Whether the granted user may only look.</param>
    /// <param name="readWrite">Whether the granted user may also act.</param>
    public ServerAccessRequest(
        string? allowUser = null,
        string? denyUser = null,
        bool list = false,
        bool readOnly = false,
        bool readWrite = false)
    {
        if (allowUser is not null && denyUser is not null)
        {
            throw new ArgumentException(
                "One request either grants access or takes it away.",
                nameof(denyUser));
        }

        if (readOnly && readWrite)
        {
            throw new ArgumentException(
                "One request either grants looking or acting, not both.",
                nameof(readWrite));
        }

        AllowUser = allowUser;
        DenyUser = denyUser;
        List = list;
        ReadOnly = readOnly;
        ReadWrite = readWrite;
    }

    /// <summary>Gets the user to grant access to.</summary>
    public string? AllowUser { get; }

    /// <summary>Gets the user to take access from.</summary>
    public string? DenyUser { get; }

    /// <summary>Gets whether the current list is reported.</summary>
    public bool List { get; }

    /// <summary>Gets whether the granted user may only look.</summary>
    public bool ReadOnly { get; }

    /// <summary>Gets whether the granted user may also act.</summary>
    public bool ReadWrite { get; }
}
