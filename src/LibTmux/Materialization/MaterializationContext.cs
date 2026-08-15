namespace LibTmux.Internal;

/// <summary>
/// Names the server that owns every entity one materialization produces.
/// </summary>
/// <remarks>
/// Ownership is explicit rather than ambient so a row parsed from one server
/// can never be attached to a handle from another, and so a generation change
/// is detected at materialization instead of at the next command.
/// </remarks>
internal sealed class MaterializationContext
{
    internal MaterializationContext(Server server, TmuxVersion tmuxVersion)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (!tmuxVersion.IsValid)
        {
            throw new ArgumentException(
                "A valid tmux version is required.",
                nameof(tmuxVersion));
        }

        Server = server;
        TmuxVersion = tmuxVersion;
    }

    /// <summary>Gets the server that owns materialized entities.</summary>
    internal Server Server { get; }

    /// <summary>Gets the tmux version that gates the projection.</summary>
    internal TmuxVersion TmuxVersion { get; }

    /// <summary>Gets the owning server generation.</summary>
    /// <exception cref="InvalidOperationException">
    /// The server has not acquired a live generation.
    /// </exception>
    internal ServerGeneration Generation =>
        Server.Generation
        ?? throw new InvalidOperationException(
            "The server has no live generation; connect before materializing.");

    /// <summary>Throws when a parsed generation is not the owning one.</summary>
    /// <param name="observed">The generation tmux reported with the row.</param>
    internal void EnsureOwns(ServerGeneration observed)
    {
        ServerGeneration expected = Generation;
        if (observed != expected)
        {
            throw new StaleServerGenerationException(
                "The tmux server generation changed during materialization.",
                expected,
                observed);
        }
    }
}
