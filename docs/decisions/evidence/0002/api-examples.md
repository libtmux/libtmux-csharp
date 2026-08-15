evaluatedCommit: 6f6b0c6debe90447d42b4c1dd4b1efd571824f43

# Model AOT API examples

## Mutable entities

Refresh updates and returns the same generation-bound entity handle.

```csharp
MutableServer server = await MutableServer.ConnectAsync(probe, cancellationToken);
MutableSession session = (await server.GetSessionsAsync(cancellationToken))[0];
MutableSession refreshed = await session.RefreshAsync(cancellationToken);

Debug.Assert(ReferenceEquals(session, refreshed));
Debug.Assert(session.Id == refreshed.Id);
IReadOnlyList<MutableWindowEdge> capturedEdges = refreshed.WindowEdges;
```

## Immutable services

Requests return replacement snapshots with explicit relation availability.

```csharp
ServerService servers = new(endpoint);
ServerSnapshot captured = await servers.CaptureAsync(
    new ServerCaptureRequest(CaptureWinlinks: true), cancellationToken);
SessionSnapshot session = captured.Sessions[0];
SessionSnapshot renamed = await new SessionService(endpoint).RenameAsync(
    session, "renamed", cancellationToken);

Debug.Assert(session.Winlinks.IsCaptured);
Debug.Assert(session.Key == renamed.Key);
Debug.Assert(session.Name != renamed.Name);
```

## Immutable hierarchy

Ordinary handles are immutable; a separately owned scope performs cleanup.

```csharp
Server server = await Server.ConnectAsync(endpoint, cancellationToken);
Server captured = await server.CaptureHierarchyAsync(cancellationToken);
Session session = captured.Sessions[0];
Session refreshed = await session.RefreshAsync(cancellationToken);

Debug.Assert(captured.Sessions.IsCaptured);
Debug.Assert(session.Key == refreshed.Key);
Debug.Assert(!ReferenceEquals(session, refreshed));
Debug.Assert(session.Windows[0].Equals(session.Winlinks[0].Window));

await using OwnedSessionScope owned = await server.CreateOwnedSessionAsync(
    "owned", cancellationToken);
Session ownedSession = owned.Session;
```
