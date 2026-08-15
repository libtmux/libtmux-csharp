using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LibTmux.UnitTests.Entities;

public sealed class EntityShellTests
{
    private static readonly string[] CanonicalEntityNames =
    [
        "Server",
        "Session",
        "Window",
        "Pane",
        "Client",
    ];

    [Fact]
    public async Task Canonical_entities_are_public_sealed_partial_before_members_are_added()
    {
        foreach (string entityName in CanonicalEntityNames)
        {
            string resourceName = $"LibTmux.UnitTests.EntitySources.{entityName}.cs";
            Stream? embeddedSource = typeof(EntityShellTests).Assembly
                .GetManifestResourceStream(resourceName);
            Assert.NotNull(embeddedSource);
            using Stream source = embeddedSource;
            using var reader = new StreamReader(source);
            string sourceText = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(
                sourceText,
                cancellationToken: TestContext.Current.CancellationToken)
                .GetCompilationUnitRoot(TestContext.Current.CancellationToken);
            ClassDeclarationSyntax declaration = Assert.Single(
                root.DescendantNodes().OfType<ClassDeclarationSyntax>());

            Assert.Equal(entityName, declaration.Identifier.ValueText);
            Assert.Contains(
                declaration.Modifiers,
                modifier => modifier.IsKind(SyntaxKind.PublicKeyword));
            Assert.Contains(
                declaration.Modifiers,
                modifier => modifier.IsKind(SyntaxKind.SealedKeyword));
            Assert.Contains(
                declaration.Modifiers,
                modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
            Assert.Empty(declaration.Members);
        }
    }
}
