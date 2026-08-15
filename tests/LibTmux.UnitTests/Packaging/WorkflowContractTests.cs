namespace LibTmux.UnitTests.Packaging;

/// <summary>Holds the workflows to what the library claims to support.</summary>
/// <remarks>
/// The README and the package say which tmux versions and which frameworks
/// this works on. A workflow that tests fewer than that turns the claim into
/// something nobody checks.
/// </remarks>
public sealed class WorkflowContractTests
{
    private static readonly string[] SupportedTmuxVersions =
        ["3.2a", "3.3a", "3.4", "3.5", "3.6", "3.7a", "3.7b"];

    private static readonly string[] TargetFrameworks = ["net8.0", "net10.0"];

    [Fact]
    public void Platform_and_macos_tmux_configurations_are_exact()
    {
        string workflow = ReadWorkflow("csharp-tmux.yml");

        foreach (string version in SupportedTmuxVersions)
        {
            Assert.Contains($"'{version}'", workflow, StringComparison.Ordinal);
        }

        foreach (string framework in TargetFrameworks)
        {
            Assert.Contains($"'{framework}'", workflow, StringComparison.Ordinal);
        }

        // One lane failing says something about that tmux version, which is
        // only readable when the other lanes still run.
        Assert.Contains("fail-fast: false", workflow, StringComparison.Ordinal);

        // A lane that silently skipped its integration tests would pass while
        // proving nothing.
        Assert.Contains("LIBTMUX_INTEGRATION_REQUIRED", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void The_build_workflow_gates_what_the_repository_gates()
    {
        string workflow = ReadWorkflow("csharp.yml");

        // These are the checks a change has to pass locally, so a change that
        // passes here and fails there would make one of them a lie.
        Assert.Contains("--locked-mode", workflow, StringComparison.Ordinal);
        Assert.Contains("--verify-no-changes", workflow, StringComparison.Ordinal);
        Assert.Contains("--warnaserror", workflow, StringComparison.Ordinal);

        foreach (string framework in TargetFrameworks)
        {
            Assert.Contains($"--framework {framework}", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_build_workflow_proves_the_package_before_publishing_it()
    {
        string workflow = ReadWorkflow("csharp.yml");

        // A package nobody has installed is a package nobody knows works.
        Assert.Contains("dotnet pack", workflow, StringComparison.Ordinal);
        Assert.Contains("LibTmux.PackageConsumer", workflow, StringComparison.Ordinal);

        // Trim and ahead-of-time warnings are only complete once something is
        // published that way and run.
        Assert.Contains("LibTmux.AotSmoke", workflow, StringComparison.Ordinal);

        // An example that stopped working should fail the build, not the
        // reader who copied it.
        Assert.Contains("LibTmux.Examples", workflow, StringComparison.Ordinal);

        // SourceLink points at the commit that produced the assembly, which a
        // shallow checkout does not have.
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void The_release_workflow_proves_the_package_before_it_is_permanent()
    {
        string workflow = ReadWorkflow("release.yml");

        // A version on nuget.org can be unlisted and never deleted, so every
        // check that could fail has to run while the package is still private.
        int inspected = workflow.IndexOf("inspect_packages.py", StringComparison.Ordinal);
        int consumed = workflow.IndexOf("LibTmux.PackageConsumer", StringComparison.Ordinal);
        int pushed = workflow.IndexOf("dotnet nuget push", StringComparison.Ordinal);

        Assert.True(inspected > 0, "The release workflow does not inspect the packages.");
        Assert.True(consumed > 0, "The release workflow does not run the package consumer.");
        Assert.True(pushed > 0, "The release workflow does not push.");
        Assert.True(inspected < pushed, "The packages are pushed before they are inspected.");
        Assert.True(consumed < pushed, "The packages are pushed before one is installed and run.");
    }

    [Fact]
    public void The_release_workflow_matches_the_trusted_publishing_policy()
    {
        string workflow = ReadWorkflow("release.yml");

        // nuget.org issues a key only for a token from the workflow file and
        // environment its policy names. Either of these drifting turns every
        // release into an authentication failure.
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("environment: nuget", workflow, StringComparison.Ordinal);
        Assert.Contains("NuGet/login", workflow, StringComparison.Ordinal);

        // A tag that disagrees with the version publishes something
        // permanently mislabelled.
        Assert.Contains("-getProperty:Version", workflow, StringComparison.Ordinal);
    }

    private static string ReadWorkflow(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, ".github", "workflows", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The workflow '{name}' was not found.");
    }
}
