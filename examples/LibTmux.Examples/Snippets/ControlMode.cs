using System.Runtime.Versioning;

namespace LibTmux.Examples.Snippets;

/// <summary>One client held open, so tmux reports what nobody asked for.</summary>
[UnsupportedOSPlatform("windows")]
public static class ControlMode
{
    /// <summary>Waits for tmux to announce the window this created.</summary>
    [Example("Hold a client open and read an event nobody asked for")]
    public static async Task WatchForWindowAdd(Server server, CancellationToken ct)
    {
        #region WatchForWindowAdd
        await using IControlModeSession control = await server.EnterControlModeAsync(cancellationToken: ct);

        await control.SendAsync("new-window -d -n build", ct);

        await foreach (TmuxEvent observed in control.Events.WithCancellation(ct))
        {
            if (observed is TmuxNotificationEvent { Name: "window-add" } added)
            {
                Console.WriteLine($"window-add {added.Arguments[0]}");
                break;
            }
        }
        #endregion
    }
}
