namespace DomainCopilot.Application.Tools;

public sealed class ToolRegistry
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(
            tool => tool.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names =>
        _tools.Keys.ToArray();

    public bool TryGet(
        string name,
        out ITool? tool)
    {
        return _tools.TryGetValue(name, out tool);
    }
}
