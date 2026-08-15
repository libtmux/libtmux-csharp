# LibTmux.Query.Json

> **Alpha.** `0.0.0-alpha.1` is the first prerelease. The public API is not
> settled and can change between prereleases without notice, so pin an exact
> version.

`System.Text.Json` support for [LibTmux](https://www.nuget.org/packages/LibTmux)
query documents. The core library does not reference it, so a caller who does
not want a JSON dependency does not get one.

```csharp
using LibTmux.Query;
using LibTmux.Query.Json;

QueryDocument document = QueryExtensions.Translate<Session>(
    session => session.Name.StartsWith("build") && session.IsAttached);

string wire = QueryJson.Serialize(document);
QueryDocument parsed = QueryJson.Deserialize(wire);
```

The wire format is versioned and its schema ships in the package as
`libtmux-query-v1.schema.json`. Reading applies the limits in
`QueryJsonLimits.V1` — depth, node count, string length — so a document that
arrived from somewhere else cannot cost more than a document is allowed to.

Documentation: <https://github.com/libtmux/libtmux-csharp>
