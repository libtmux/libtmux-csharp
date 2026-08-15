evaluatedCommit: 953a1970d91bbe319906a8a2e294799eb4b966ca

# Query AOT API examples

All catalogs consume the same expression, AST, JSON, and direct interpreter.

```csharp
Expression<Func<SessionSnapshot, bool>> predicate = session =>
    session.Attached
    && session.Windows.Any(window =>
        window.Panes.Any(pane => pane.Command == "nvim"));

IFieldCatalogContender catalog = new GeneratedFieldCatalog();
QueryDocument document = QueryTranslator.Translate(predicate, catalog);
string json = QueryJson.Serialize(document, catalog);
QueryDocument roundTripped = QueryJson.Deserialize(json, catalog);
Func<SessionSnapshot, bool> direct =
    QueryInterpreter.Compile<SessionSnapshot>(
        roundTripped, catalog, preferDynamicCode: false);
```

The Attributes, Static, and Generated lanes instantiate only their selected catalog.
