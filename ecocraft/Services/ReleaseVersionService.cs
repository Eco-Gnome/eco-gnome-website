namespace ecocraft.Services;

public class ReleaseVersion(string title, string date, string description, string changes)
{
    public string Title { get; private set; } = title;
    public string Date { get; private set; } = date;
    public string Description { get; private set; } = description;
    public string Changes { get; private set; } = changes;
}

public class ReleaseVersionService
{
    public static List<ReleaseVersion> ReleaseVersions =
    [
        new ReleaseVersion(
            "0.1.0",
            "2024-11-18",
            "Official first beta version",
            """
            - Price Calculator: calculate all prices of Eco Items, with advanced behaviours
            - Graph View: visualize your chain of production
            - User Management: Save your configuration online with simple account management
            - Server Management: Export your server data thanks to our [EcoGnomeMod](https://github.com/Eco-Gnome/eco-gnome-mod), and import your specific recipes and configurations
            """
        ),
    ];
}
