# LibTmux.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that gives
an assistant hands on tmux, built on
[LibTmux](https://www.nuget.org/packages/LibTmux).

> **Alpha.** The public API is not settled and can change between prereleases
> without notice, so pin an exact version.

This is a **.NET tool**, not a library reference:

```console
$ dotnet tool install --global LibTmux.Mcp --prerelease
```

## Point a client at it

It speaks the protocol over standard input and output, which is how an MCP
client starts it:

```json
{
  "mcpServers": {
    "tmux": {
      "command": "libtmux-mcp"
    }
  }
}
```

To drive a server other than the ambient one — a sandbox, a test rig, a
long-lived project socket — pass a socket name:

```json
{
  "mcpServers": {
    "tmux": {
      "command": "libtmux-mcp",
      "args": ["my-socket"]
    }
  }
}
```

## What the assistant gets

| Tool | Does |
|---|---|
| `list_tmux` | Reads the hierarchy: sessions, windows, panes |
| `create_tmux_session` | Starts a session |
| `create_tmux_window` | Adds a window to a session |
| `run_in_tmux_pane` | Sends a command to a pane and returns what it produced |
| `capture_tmux_pane` | Reads a pane's visible content or scrollback |

Confirm what a build exposes without a client:

```console
$ printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}' | libtmux-mcp
```

## Standard output belongs to the protocol

Every log line goes to standard error. A message written to the wrong stream
does not produce a stray log line — it corrupts the protocol and the client
disconnects. That is worth knowing if you wrap this in something of your own.

## Which tmux it drives

Whatever `tmux` resolves to on the path, or the binary `LIBTMUX_TMUX` names.
The supported range is 3.2a to 3.7b, proven from source on every commit.

## Related packages

| Package | Adds |
|---|---|
| [LibTmux](https://www.nuget.org/packages/LibTmux) | The client this is built on |
| [LibTmux.Query.Json](https://www.nuget.org/packages/LibTmux.Query.Json) | JSON for query documents |
| [LibTmux.Workspace](https://www.nuget.org/packages/LibTmux.Workspace) | Sessions from tmuxp YAML |

Source, docs and issues: <https://github.com/libtmux/libtmux-csharp>

## License

[MIT](https://github.com/libtmux/libtmux-csharp/blob/master/LICENSE)
