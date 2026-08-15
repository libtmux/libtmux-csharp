# LibTmux.Workspace

Builds tmux sessions from [tmuxp](https://github.com/tmux-python/tmuxp)
workspace files, on top of [LibTmux](https://www.nuget.org/packages/LibTmux).

```csharp
using LibTmux;
using LibTmux.Workspace;

Server server = await Server.ConnectAsync();
WorkspaceFile workspace = WorkspaceFile.Parse(File.ReadAllText("session.yaml"));

WorkspaceResult result = await new WorkspaceBuilder(server).BuildAsync(workspace);
```

The result says what was built and what could not be, rather than throwing away
a session because one pane's command was wrong.

This reads the workspace shape tmuxp writes — session name, windows, panes,
layouts, working directories, and the commands to send. It is not a tmuxp
runtime: what tmuxp does with plugins, before/after hooks, and its own
configuration search path is out of scope.

Documentation: <https://github.com/libtmux/libtmux-csharp>
