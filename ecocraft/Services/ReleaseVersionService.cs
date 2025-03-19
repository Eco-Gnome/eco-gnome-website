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
            "0.3.0",
            "2025-03-19",
            "Official third beta version",
            """
            - Translations: Website is now translated to a lot of languages thanks to AI. Contribute to improve them !
            - Margin Calculation: You can now apply margin prices between skills, allowing more fair prices between citizens who buy items vs citizens who produce them.
            - Default Prices: Server admin may now define default prices, which are set on item by default. Users can now see this configuration.
            """
        ),
        new ReleaseVersion(
            "0.2.0",
            "2024-12-09",
            "Official second beta version",
            """
            - UI Improvements: UI has been improved to increase readability and usability of the website
            - Eco Sync: Synchronize your prices in-game thanks to a chat command of the [EcoGnomeMod](https://github.com/Eco-Gnome/eco-gnome-mod)
            - Translations: Website is designed to be translated in all Eco-supported languages. Translations are still WIP.
            - Margin Calculation: You can now select between two types of margin calculation: MarkUp or GrossMargin
            """
        ),
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
