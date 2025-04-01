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
            "1.0.0-rc1",
            "2025-04-02",
            "1.0.0 Release Candidate 1",
            """
            - Server Data Copy: You can now import server data from an other server you have joined. It allows you to specify your own min/default/max prices on a server you play on, in your settlement, for instance.
            - Default Reintegration: Recipes that obviously require products reintegration (molds, barrels, ...) now reintegrate these products by default.
            - Default Share: Recipes that produce multiple items now offers 80% share for the first product, and the rest share the remaining 20%.
            - Source Code & Contacts: Eco Gnome now displays the source code link and a way to contact its creators.
            - Minor improvements: Better header loading, Button improvements in Server Admin page, small bug fixes...
            """
        ),
        new ReleaseVersion(
            "0.3.0",
            "2025-03-19",
            "Official third beta version",
            """
            - Translations: The website has now been translated into multiple languages using AI. Feel free to help improve them by contributing on Github!
            - Margin Calculation: You can now apply margin pricing between skills, ensuring fairer prices between citizens who buy items and those who produce them.
            - Default Prices: Server admins can now set default item prices, which will apply automatically. Users can now view this configuration.
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
