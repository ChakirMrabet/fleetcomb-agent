namespace FleetComb.Agent.Ui;

public sealed class UiAssemblyMarker;

public static class UiAssets
{
    public static string Css { get; } = Read("FleetComb.Agent.Ui.agent.css");

    private static string Read(string name)
    {
        using var stream = typeof(UiAssets).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded UI asset '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
