# LibTmux.Mcp

> **Alpha.** `0.0.0-alpha.1` is the first prerelease. The public API is not
> settled and can change between prereleases without notice, so pin an exact
> version.

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets an
assistant drive tmux through
[LibTmux](https://www.nuget.org/packages/LibTmux).

```console
dotnet tool install --global LibTmux.Mcp
```

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

Pass a socket name as the first argument to drive a server other than the
ambient one, which is what a sandbox or a test wants:

```console
libtmux-mcp my-socket
```

Every log line goes to standard error. The protocol owns standard output, so a
message written to the wrong stream does not produce a stray log line — it
corrupts the protocol and the client disconnects.

Documentation: <https://github.com/libtmux/libtmux-csharp>
