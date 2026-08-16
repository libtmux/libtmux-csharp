namespace LibTmux.Examples;

/// <summary>Marks a method as an example that runs against a live tmux server.</summary>
/// <remarks>
/// The method name is the example's identity: it names the test that runs it
/// and, when the method carries a <c>#region</c> of the same name, the block a
/// document publishes. Nothing repeats that name in a table somewhere, so an
/// example cannot be renamed into a document that no longer points at it.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExampleAttribute : Attribute
{
    /// <summary>Initializes the attribute.</summary>
    /// <param name="title">One line saying what the example shows.</param>
    public ExampleAttribute(string title) => Title = title;

    /// <summary>Gets the line saying what the example shows.</summary>
    public string Title { get; }
}
