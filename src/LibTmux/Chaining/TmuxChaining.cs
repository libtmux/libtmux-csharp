using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Turns a request record into a command a chain can carry.</summary>
/// <remarks>
/// <para>
/// Every overload builds its command with the same code the one-shot method
/// uses, so a chained call and a direct call send identical arguments rather
/// than two descriptions that have to be kept in step.
/// </para>
/// <para>
/// What each overload takes is decided by what tmux needs, not by taste. A
/// session names no target because the session is what is being made; a window
/// names the session that will hold it; keys name the pane, because which
/// flags tmux accepts depends on the server version and the pane is what knows
/// it.
/// </para>
/// </remarks>
public static class TmuxChaining
{
    /// <summary>Returns a session request as one tmux command.</summary>
    /// <param name="request">The session to create.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this NewSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildNewSessionArguments(request)]);
    }

    /// <summary>Returns a window request as one tmux command.</summary>
    /// <param name="request">The window to create.</param>
    /// <param name="target">The session the window is created in.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="target" /> is empty.</exception>
    public static TmuxCommand ToCommand(this NewWindowRequest request, string target)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(target);
        return Command([.. Session.BuildNewWindowArguments(request, target)]);
    }

    /// <summary>Returns a key request as one tmux command for a pane.</summary>
    /// <param name="request">The keys to send.</param>
    /// <param name="pane">The pane that receives them.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this SendKeysRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);

        // The pane ID travels into the chain as plain text, so RequiredGeneration
        // pins it: after a restart, that ID could name a different pane.
        return Command([.. pane.BuildSendKeysArguments(request)]) with
        {
            RequiredGeneration = pane.Generation,
        };
    }

    /// <summary>Returns a key-binding request as one tmux command.</summary>
    /// <param name="request">The binding to add.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this BindKeyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildBindKeyArguments(request)]);
    }

    /// <summary>Returns a key-unbinding request as one tmux command.</summary>
    /// <param name="request">The binding to remove.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this UnbindKeyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildUnbindKeyArguments(request)]);
    }

    /// <summary>Runs a key-binding request on its own.</summary>
    /// <param name="request">The binding to add.</param>
    /// <param name="server">The server to bind on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary bind.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this BindKeyRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Runs a key-unbinding request on its own.</summary>
    /// <param name="request">The binding to remove.</param>
    /// <param name="server">The server to unbind on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary unbind.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this UnbindKeyRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a layout request as one tmux command for a window.</summary>
    /// <param name="request">The layout to apply.</param>
    /// <param name="window">The window the layout applies to.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// This takes the window because a layout name is checked against the ones
    /// the running tmux knows, and an unrecognised name takes the whole server
    /// down on tmux 3.3a. Batching a layout must not skip that check.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxWindowException">The layout is one tmux may not recognise.</exception>
    public static TmuxCommand ToCommand(this SelectLayoutRequest request, Window window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(window);
        return Command([.. window.BuildSelectLayoutArguments(request)]);
    }

    /// <summary>Runs a layout request on its own.</summary>
    /// <param name="request">The layout to apply.</param>
    /// <param name="window">The window the layout applies to.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary layout.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SelectLayoutRequest request,
        Window window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Server
            .Chain()
            .Then(request.ToCommand(window))
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a conditional request as one tmux command.</summary>
    /// <param name="request">What to run.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this IfShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildIfShellArguments(request)]);
    }

    /// <summary>Runs a conditional request on its own.</summary>
    /// <param name="request">What to run.</param>
    /// <param name="server">The server to run it on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this IfShellRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a channel request as one tmux command.</summary>
    /// <param name="request">What to run.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this WaitForRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildWaitForArguments(request)]);
    }

    /// <summary>Runs a channel request on its own.</summary>
    /// <param name="request">What to run.</param>
    /// <param name="server">The server to run it on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this WaitForRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a pane-selection request as one tmux command.</summary>
    /// <param name="request">Which pane to select, and how.</param>
    /// <param name="pane">The pane the selection is relative to.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this SelectPaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildSelectPaneArguments(request)]);
    }

    /// <summary>Runs a pane-selection request on its own.</summary>
    /// <param name="request">Which pane to select, and how.</param>
    /// <param name="pane">The pane the selection is relative to.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary selection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SelectPaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a pane-resize request as one tmux command.</summary>
    /// <param name="request">How to resize.</param>
    /// <param name="pane">The pane being resized.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this ResizePaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildResizePaneArguments(request)]);
    }

    /// <summary>Runs a pane-resize request on its own.</summary>
    /// <param name="request">How to resize.</param>
    /// <param name="pane">The pane being resized.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary resize.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ResizePaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a window-search request as one tmux command.</summary>
    /// <param name="request">What to look for.</param>
    /// <param name="pane">The pane the search starts from.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this FindWindowRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildFindWindowArguments(request)]);
    }

    /// <summary>Runs a window-search request on its own.</summary>
    /// <param name="request">What to look for.</param>
    /// <param name="pane">The pane the search starts from.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary search.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this FindWindowRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a pane-swap request as one tmux command.</summary>
    /// <param name="request">Which pane to swap with, and how.</param>
    /// <param name="pane">The pane being swapped.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this SwapPaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildSwapPaneArguments(request)]);
    }

    /// <summary>Runs a pane-swap request on its own.</summary>
    /// <param name="request">Which pane to swap with, and how.</param>
    /// <param name="pane">The pane being swapped.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary swap.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SwapPaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a pane-piping request as one tmux command.</summary>
    /// <param name="request">What to pipe, and which way.</param>
    /// <param name="pane">The pane being piped.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this PipePaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildPipePaneArguments(request)]);
    }

    /// <summary>Runs a pane-piping request on its own.</summary>
    /// <param name="request">What to pipe, and which way.</param>
    /// <param name="pane">The pane being piped.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary pipe.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this PipePaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a capture request as one tmux command.</summary>
    /// <param name="request">What to capture.</param>
    /// <param name="pane">The pane being captured.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Several capture flags arrived after tmux 3.2a, and the pane is what
    /// knows which tmux is answering, so the command it builds carries only
    /// the flags that server accepts.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this CapturePaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildCaptureArguments(["-p"], request)]);
    }

    /// <summary>Runs a capture request on its own.</summary>
    /// <param name="request">What to capture.</param>
    /// <param name="pane">The pane being captured.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is the captured text.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this CapturePaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a message request as one tmux command.</summary>
    /// <param name="request">What to show, and where.</param>
    /// <param name="server">The server the message is shown on.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// This takes the server because two of the flags depend on which tmux is
    /// answering: literal expansion arrived in 3.4, and 3.2a refuses the
    /// target-client flag even for a client that is really attached.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this DisplayMessageRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildDisplayMessageArguments(request)]);
    }

    /// <summary>Runs a message request on its own.</summary>
    /// <param name="request">What to show, and where.</param>
    /// <param name="server">The server the message is shown on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is the message when it was asked for.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this DisplayMessageRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a shell request as one tmux command.</summary>
    /// <param name="request">What to run, and how.</param>
    /// <param name="server">The server that runs it.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Three of this command's flags arrived at different tmux versions, so
    /// the server is what decides which of them the built command carries.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this RunShellRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildRunShellArguments(request)]);
    }

    /// <summary>Runs a shell request on its own.</summary>
    /// <param name="request">What to run, and how.</param>
    /// <param name="server">The server that runs it.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is the command's output.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this RunShellRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a paste request as one tmux command.</summary>
    /// <param name="request">Which buffer to paste, and how.</param>
    /// <param name="pane">The pane being pasted into.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Pasting raw bytes arrived in tmux 3.7, so the pane decides whether the
    /// built command carries that flag.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this PasteBufferRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildPasteBufferArguments(request)]);
    }

    /// <summary>Runs a paste request on its own.</summary>
    /// <param name="request">Which buffer to paste, and how.</param>
    /// <param name="pane">The pane being pasted into.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary paste.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this PasteBufferRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a menu request as one tmux command.</summary>
    /// <param name="request">What the menu offers, and how it looks.</param>
    /// <param name="server">The server the menu is shown on.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// The style flags arrived in tmux 3.4 and the mouse flag in 3.5, so the
    /// server decides which of them the built command carries.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this DisplayMenuRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildDisplayMenuArguments(request)]);
    }

    /// <summary>Runs a menu request on its own.</summary>
    /// <param name="request">What the menu offers, and how it looks.</param>
    /// <param name="server">The server the menu is shown on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary menu.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this DisplayMenuRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a popup request as one tmux command.</summary>
    /// <param name="request">What the popup shows, and where.</param>
    /// <param name="pane">The pane the popup belongs to.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Popup options arrived in tmux 3.3 and the key policy in 3.6, so the
    /// pane decides which of them the built command carries.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this DisplayPopupRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildDisplayPopupArguments(request)]);
    }

    /// <summary>Runs a popup request on its own.</summary>
    /// <param name="request">What the popup shows, and where.</param>
    /// <param name="pane">The pane the popup belongs to.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary popup.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this DisplayPopupRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns an option request as one tmux command.</summary>
    /// <param name="request">Which option to set, and to what.</param>
    /// <param name="options">The options handle whose scope the option is set in.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// This takes the options handle rather than a server, because which
    /// scope flags and target tmux receives follow from the handle the caller
    /// reached for: a window's options and a server's are the same request
    /// spelled differently.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this SetOptionRequest request, TmuxOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return Command([.. options.BuildSetArguments(request)]);
    }

    /// <summary>Runs an option request on its own.</summary>
    /// <param name="request">Which option to set, and to what.</param>
    /// <param name="options">The options handle whose scope the option is set in.</param>
    /// <param name="server">The server the option is set on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary set.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SetOptionRequest request,
        TmuxOptions options,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(options)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns an unset request as one tmux command.</summary>
    /// <param name="request">Which option to unset, and how.</param>
    /// <param name="options">The options handle whose scope the option is unset in.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this UnsetOptionRequest request, TmuxOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return Command([.. options.BuildUnsetArguments(request)]);
    }

    /// <summary>Runs an unset request on its own.</summary>
    /// <param name="request">Which option to unset, and how.</param>
    /// <param name="options">The options handle whose scope the option is unset in.</param>
    /// <param name="server">The server the option is unset on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary unset.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this UnsetOptionRequest request,
        TmuxOptions options,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(options)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a hook request as one tmux command.</summary>
    /// <param name="request">Which hook to set, and to what.</param>
    /// <param name="hooks">The hooks handle whose scope the hook is set in.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this SetHookRequest request, TmuxHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hooks);
        return Command([.. hooks.BuildSetArguments(request)]);
    }

    /// <summary>Runs a hook request on its own.</summary>
    /// <param name="request">Which hook to set, and to what.</param>
    /// <param name="hooks">The hooks handle whose scope the hook is set in.</param>
    /// <param name="server">The server the hook is set on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary hook.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SetHookRequest request,
        TmuxHooks hooks,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(hooks)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a confirmation request as one tmux command.</summary>
    /// <param name="request">What to confirm, and what to run when it is.</param>
    /// <param name="server">The server the confirmation is shown on.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Naming the accepting key, and defaulting to yes, arrived in tmux 3.4,
    /// so the server decides whether the built command carries them.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this ConfirmBeforeRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildConfirmBeforeArguments(request)]);
    }

    /// <summary>Runs a confirmation request on its own.</summary>
    /// <param name="request">What to confirm, and what to run when it is.</param>
    /// <param name="server">The server the confirmation is shown on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary confirmation.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ConfirmBeforeRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a prompt request as one tmux command.</summary>
    /// <param name="request">What to ask, and how.</param>
    /// <param name="server">The server the prompt is shown on.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Batching does not soften the refusal below tmux 3.3: that version reads
    /// the type flag as something else, so a prompt asking for one is refused
    /// here exactly as it is when run alone.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxVersionTooLowException">
    /// The request asks for a format or a prompt type and tmux is older than 3.3.
    /// </exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this CommandPromptRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildCommandPromptArguments(request)]);
    }

    /// <summary>Runs a prompt request on its own.</summary>
    /// <param name="request">What to ask, and how.</param>
    /// <param name="server">The server the prompt is shown on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary prompt.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this CommandPromptRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a copy-mode request as one tmux command.</summary>
    /// <param name="request">How to enter copy mode, or whether to leave it.</param>
    /// <param name="pane">The pane entering copy mode.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Paging down on entry arrived in tmux 3.5, so the pane decides whether
    /// the built command carries that flag.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this CopyModeRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildCopyModeArguments(request)]);
    }

    /// <summary>Runs a copy-mode request on its own.</summary>
    /// <param name="request">How to enter copy mode, or whether to leave it.</param>
    /// <param name="pane">The pane entering copy mode.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary entry.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this CopyModeRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a window-resize request as one tmux command.</summary>
    /// <param name="request">The size to apply.</param>
    /// <param name="window">The window being resized.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this ResizeWindowRequest request, Window window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(window);
        return Command([.. window.BuildResizeWindowArguments(request)]);
    }

    /// <summary>Runs a window-resize request on its own.</summary>
    /// <param name="request">The size to apply.</param>
    /// <param name="window">The window being resized.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary resize.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ResizeWindowRequest request,
        Window window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Server.Chain().Then(request.ToCommand(window)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a respawn request as one tmux command for a pane.</summary>
    /// <param name="request">What to respawn, and how.</param>
    /// <param name="pane">The pane being respawned.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this RespawnRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildRespawnPaneArguments(request)]);
    }

    /// <summary>Runs a respawn request on its own.</summary>
    /// <param name="request">What to respawn, and how.</param>
    /// <param name="pane">The pane being respawned.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary respawn.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this RespawnRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a chooser request as one tmux command.</summary>
    /// <param name="request">What the chooser shows, and how it is ordered.</param>
    /// <param name="pane">The pane the chooser opens in.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// tmux 3.7 dropped the activity-time sort order and rejects it by name,
    /// so the pane decides whether the built command carries it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this ChooseTreeRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildChooseTreeArguments(request)]);
    }

    /// <summary>Runs a chooser request on its own.</summary>
    /// <param name="request">What the chooser shows, and how it is ordered.</param>
    /// <param name="pane">The pane the chooser opens in.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary chooser.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ChooseTreeRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns an access request as one tmux command.</summary>
    /// <param name="request">Whose access to change, and how.</param>
    /// <param name="server">The server whose access is changed.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// The command itself arrived in tmux 3.3, so batching does not soften the
    /// refusal below that: an older server has nothing to send it to.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxVersionTooLowException">tmux is older than 3.3.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this ServerAccessRequest request, Server server)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(server);
        return Command([.. server.BuildServerAccessArguments(request)]);
    }

    /// <summary>Runs an access request on its own.</summary>
    /// <param name="request">Whose access to change, and how.</param>
    /// <param name="server">The server whose access is changed.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which lists the users when it was asked to.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ServerAccessRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(server)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a buffer-listing request as one tmux command.</summary>
    /// <param name="request">How the buffers are rendered and filtered.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    public static TmuxCommand ToCommand(this ListBuffersRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Command([.. Server.BuildListBuffersArguments(request)]);
    }

    /// <summary>Runs a buffer-listing request on its own.</summary>
    /// <param name="request">How the buffers are rendered and filtered.</param>
    /// <param name="server">The server whose buffers are listed.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is one line per buffer.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ListBuffersRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a link request as one tmux command.</summary>
    /// <param name="request">Where the link goes.</param>
    /// <param name="window">The window being linked.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// This takes the window because the link's source is the session that
    /// window was read through, which a window resolved by identifier alone
    /// does not know.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="IncompleteSnapshotException">
    /// The window was resolved by identifier, so its source link is unknown.
    /// </exception>
    public static TmuxCommand ToCommand(this LinkWindowRequest request, Window window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(window);
        return Command([.. window.BuildLinkWindowArguments(request)]);
    }

    /// <summary>Runs a link request on its own.</summary>
    /// <param name="request">Where the link goes.</param>
    /// <param name="window">The window being linked.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary link.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this LinkWindowRequest request,
        Window window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Server.Chain().Then(request.ToCommand(window)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a window-move request as one tmux command.</summary>
    /// <param name="request">Where the window goes.</param>
    /// <param name="window">The window being moved.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this MoveWindowRequest request, Window window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(window);
        return Command([.. window.BuildMoveWindowArguments(request)]);
    }

    /// <summary>Runs a window-move request on its own.</summary>
    /// <param name="request">Where the window goes.</param>
    /// <param name="window">The window being moved.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary move.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this MoveWindowRequest request,
        Window window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Server.Chain().Then(request.ToCommand(window)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a pane-move request as one tmux command.</summary>
    /// <param name="request">Where the pane goes.</param>
    /// <param name="pane">The pane being moved.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this MovePaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildRehomeArguments("move-pane", request)]);
    }

    /// <summary>Runs a pane-move request on its own.</summary>
    /// <param name="request">Where the pane goes.</param>
    /// <param name="pane">The pane being moved.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary move.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this MovePaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a split request as one tmux command.</summary>
    /// <param name="request">How to split.</param>
    /// <param name="pane">The pane being split.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// Splitting into an empty pane arrived in tmux 3.7 and the appearance
    /// flags in 3.6, so the pane decides which of them the built command
    /// carries. It prints the new pane's identifier the same way the one-shot
    /// path does.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this SplitPaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildSplitArguments(request)]);
    }

    /// <summary>Runs a split request on its own.</summary>
    /// <param name="request">How to split.</param>
    /// <param name="pane">The pane being split.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which names the created pane.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SplitPaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a floating-pane request as one tmux command.</summary>
    /// <param name="request">How the pane floats.</param>
    /// <param name="pane">The pane the new one is created from.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// The command arrived whole in tmux 3.7, so batching does not soften the
    /// refusal below that: an older server has nothing to send it to.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxVersionTooLowException">tmux is older than 3.7.</exception>
    public static TmuxCommand ToCommand(this NewPaneRequest request, Pane pane)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pane);
        return Command([.. pane.BuildNewPaneArguments(request)]);
    }

    /// <summary>Runs a floating-pane request on its own.</summary>
    /// <param name="request">How the pane floats.</param>
    /// <param name="pane">The pane the new one is created from.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which names the created pane.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this NewPaneRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns an attach request as one tmux command.</summary>
    /// <param name="request">How to attach.</param>
    /// <param name="session">The session being attached to.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TmuxCommand ToCommand(this AttachSessionRequest request, Session session)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return Command([.. Session.BuildAttachArguments(request, session.Id.ToString())]);
    }

    /// <summary>Runs an attach request on its own.</summary>
    /// <param name="request">How to attach.</param>
    /// <param name="session">The session being attached to.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed.</returns>
    /// <remarks>
    /// Attaching needs a terminal, so this fails from a process that has none.
    /// It is here because a chain that switches a client between sessions is
    /// built the same way.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this AttachSessionRequest request,
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Server
            .Chain()
            .Then(request.ToCommand(session))
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a named option read as one tmux command.</summary>
    /// <param name="request">Which option to read.</param>
    /// <param name="options">The options handle whose scope is read.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// A chain returns one combined output stream, so several reads batched
    /// together arrive undelimited. Reach for this to read something beside
    /// the changes a chain makes; reach for the handle's own accessor when
    /// what you want is a parsed value.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this GetOptionRequest request, TmuxOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return Command([.. options.BuildGetArguments(request)]);
    }

    /// <summary>Runs a named option read on its own.</summary>
    /// <param name="request">Which option to read.</param>
    /// <param name="options">The options handle whose scope is read.</param>
    /// <param name="server">The server the option is read from.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, unparsed.</returns>
    /// <remarks>
    /// <see cref="TmuxOptions.GetAsync" /> answers the same question with the
    /// value already parsed, and is what most callers want.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this GetOptionRequest request,
        TmuxOptions options,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(options)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a whole-scope option read as one tmux command.</summary>
    /// <param name="request">How the scope is read.</param>
    /// <param name="options">The options handle whose scope is read.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this GetOptionsRequest request, TmuxOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return Command([.. options.BuildGetAllArguments(request)]);
    }

    /// <summary>Runs a whole-scope option read on its own.</summary>
    /// <param name="request">How the scope is read.</param>
    /// <param name="options">The options handle whose scope is read.</param>
    /// <param name="server">The server the options are read from.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, unparsed.</returns>
    /// <remarks>
    /// <see cref="TmuxOptions.GetAllAsync" /> answers the same question with
    /// the values already parsed, and is what most callers want.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this GetOptionsRequest request,
        TmuxOptions options,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(options)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns a hook listing as one tmux command.</summary>
    /// <param name="request">Which scope to list.</param>
    /// <param name="hooks">The hooks handle whose scope is listed.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// A chain returns one combined output stream, so batch a listing to see
    /// what the same invocation just installed. <see cref="TmuxHooks.GetAllAsync" />
    /// answers the same question with the hooks already parsed.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToCommand(this ListHooksRequest request, TmuxHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hooks);
        return Command([.. hooks.BuildListArguments(request)]);
    }

    /// <summary>Runs a hook listing on its own.</summary>
    /// <param name="request">Which scope to list.</param>
    /// <param name="hooks">The hooks handle whose scope is listed.</param>
    /// <param name="server">The server the hooks are read from.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, unparsed.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this ListHooksRequest request,
        TmuxHooks hooks,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand(hooks)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns running a hook as one tmux command.</summary>
    /// <param name="request">Which hook to run.</param>
    /// <param name="hooks">The hooks handle whose scope holds it.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <remarks>
    /// A hook request names a hook without saying what to do with it, so
    /// running and removing are separate here rather than one call that has to
    /// guess which was meant.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToRunCommand(this HookRequest request, TmuxHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hooks);
        return Command([.. hooks.BuildRunArguments(request)]);
    }

    /// <summary>Returns removing a hook as one tmux command.</summary>
    /// <param name="request">Which hook to remove.</param>
    /// <param name="hooks">The hooks handle whose scope holds it.</param>
    /// <returns>The command, ready to add to a <see cref="TmuxChain" />.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static TmuxCommand ToUnsetCommand(this HookRequest request, TmuxHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hooks);
        return Command([.. hooks.BuildUnsetArguments(request)]);
    }

    /// <summary>Runs a hook on its own.</summary>
    /// <param name="request">Which hook to run.</param>
    /// <param name="hooks">The hooks handle whose scope holds it.</param>
    /// <param name="server">The server the hook runs on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary run.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this HookRequest request,
        TmuxHooks hooks,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToRunCommand(hooks)).ExecuteAsync(cancellationToken);
    }

    /// <summary>Returns every command a multi-entry hook request sends.</summary>
    /// <param name="request">Which hook to set, and to what entries.</param>
    /// <param name="hooks">The hooks handle whose scope holds it.</param>
    /// <returns>The commands, in the order tmux must receive them.</returns>
    /// <remarks>
    /// This request is several tmux commands rather than one, so it answers a
    /// list. Running them one at a time is what the one-shot path does; adding
    /// them to a chain is what this is for.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    [UnsupportedOSPlatform("windows")]
    public static IReadOnlyList<TmuxCommand> ToCommands(
        this SetHooksRequest request,
        TmuxHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hooks);
        return [.. hooks.BuildSetAllArguments(request).Select(arguments => Command([.. arguments]))];
    }

    /// <summary>Runs a multi-entry hook request in one invocation.</summary>
    /// <param name="request">Which hook to set, and to what entries.</param>
    /// <param name="hooks">The hooks handle whose scope holds it.</param>
    /// <param name="server">The server the hook is set on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What that one invocation produced.</returns>
    /// <remarks>
    /// The one-shot path sends these one process at a time, so this is the
    /// case batching helps most.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the run failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SetHooksRequest request,
        TmuxHooks hooks,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        TmuxChain chain = server.Chain();
        foreach (TmuxCommand command in request.ToCommands(hooks))
        {
            chain = chain.Then(command);
        }

        return chain.ExecuteAsync(cancellationToken);
    }

    /// <summary>Runs a session request on its own.</summary>
    /// <param name="request">The session to create.</param>
    /// <param name="server">The server to create it on.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which names the created session.</returns>
    /// <remarks>
    /// This runs the same command <see cref="ToCommand(NewSessionRequest)" />
    /// builds, so a request executed on its own and the same request added to
    /// a chain do the same thing. Reach for the chain when there is more than
    /// one command; a single request costs the same either way.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this NewSessionRequest request,
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.Chain().Then(request.ToCommand()).ExecuteAsync(cancellationToken);
    }

    /// <summary>Runs a window request on its own.</summary>
    /// <param name="request">The window to create.</param>
    /// <param name="session">The session that will hold it.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which names the created window.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this NewWindowRequest request,
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Server
            .Chain()
            .Then(request.ToCommand(session.Id.ToString()))
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>Runs a key request on its own.</summary>
    /// <param name="request">The keys to send.</param>
    /// <param name="pane">The pane that receives them.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux printed, which is nothing for an ordinary send.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the command failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public static Task<TmuxCommandResult> ExecuteAsync(
        this SendKeysRequest request,
        Pane pane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pane);
        return pane.Server.Chain().Then(request.ToCommand(pane)).ExecuteAsync(cancellationToken);
    }

    private static TmuxCommand Command(string[] arguments) =>
        new(arguments[0], arguments[1..]);
}
