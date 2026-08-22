using System.ComponentModel;
using System.Reflection;
using System.Runtime.Versioning;
using LibTmux.Mcp;

namespace LibTmux.UnitTests.Mcp;

[UnsupportedOSPlatform("windows")]
public sealed class RecipePromptTests
{
    [Fact]
    public void Recipes_use_the_live_camel_case_schema_names()
    {
        string prompts = string.Join(
            '\n',
            RecipePrompts.RunAndReport("true", "%1"),
            RecipePrompts.DiagnosePane("%1"),
            RecipePrompts.BuildWorkspace("work"),
            RecipePrompts.InterruptPane("%1"));

        foreach (string stale in new[]
        {
            "exit_status",
            "timed_out",
            "pane_id",
            "current_command",
            "alternate_screen",
            "max_lines",
            "timeout_seconds",
        })
        {
            Assert.DoesNotContain(stale, prompts, StringComparison.Ordinal);
        }

        Assert.Contains("exitStatus", prompts, StringComparison.Ordinal);
        Assert.Contains("timedOut", prompts, StringComparison.Ordinal);
        Assert.Contains("pane.currentCommand", prompts, StringComparison.Ordinal);
        Assert.Contains("alternateScreen", prompts, StringComparison.Ordinal);
        Assert.Contains("paneId=", prompts, StringComparison.Ordinal);
        Assert.Contains("maxLines", prompts, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_descriptions_use_the_live_camel_case_result_names()
    {
        string descriptions = string.Join(
            '\n',
            Describe(typeof(WriteTools), nameof(WriteTools.RunAsync)),
            Describe(typeof(ReadTools), nameof(ReadTools.WaitForTextAsync)),
            Describe(typeof(ReadTools), nameof(ReadTools.HierarchyAsync)),
            Describe(typeof(ReadTools), nameof(ReadTools.ListPanesAsync)),
            Describe(typeof(WriteTools), nameof(WriteTools.CancelJobAsync)));

        foreach (string stale in new[]
        {
            "lines_missed",
            "anchor_lost",
            "effective_timeout_seconds",
            "is_caller",
            "current_command",
        })
        {
            Assert.DoesNotContain(stale, descriptions, StringComparison.Ordinal);
        }

        Assert.Contains("linesMissed", descriptions, StringComparison.Ordinal);
        Assert.Contains("anchorLost", descriptions, StringComparison.Ordinal);
        Assert.Contains("effectiveTimeoutSeconds", descriptions, StringComparison.Ordinal);
        Assert.Contains("isCaller", descriptions, StringComparison.Ordinal);
        Assert.Contains("currentCommand", descriptions, StringComparison.Ordinal);
    }

    private static string Describe(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method {type.Name}.{methodName} is absent.");
        IEnumerable<string> descriptions = method
            .GetCustomAttributes<DescriptionAttribute>()
            .Select(static attribute => attribute.Description)
            .Concat(method.GetParameters().SelectMany(static parameter => parameter
                .GetCustomAttributes<DescriptionAttribute>()
                .Select(static attribute => attribute.Description)));
        return string.Join('\n', descriptions);
    }
}
