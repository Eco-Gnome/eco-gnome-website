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
            "1.0.0",
            "2025-04-15",
            "1.0.0",
            """
            - Talents: EcoGnome now retrieves talents from your server. You can select them once the required level is reached.
            - Modules: When using the level 4 plugin, you can now select multiple specialized modules.
            - Warning: These two changes require updating EcoGnomeMod and re-uploading your server data using the newly generated file.
            - Minor Enhancements: Icons are now displayed in search results.
            """
        ),
        new ReleaseVersion(
            "1.0.0-rc1",
            "2025-04-02",
            "1.0.0 Release Candidate 1",
            """
            - Server Data Import: You can now import data from other servers you’ve joined. This allows you to define your own min/default/max prices for use on a different server — for example, in your own settlement.
            - Automatic Reintegration: Recipes that logically require reintegration of input items (e.g., molds, barrels) now do so automatically by default.
            - Default Value Sharing: For recipes that yield multiple products, 80% of the value is now assigned to the first item, with the remaining 20% distributed across the others.
            - Source Code & Contact: Eco Gnome now includes a link to its source code and a way to contact the development team.
            - Graph View: This feature has been temporarily hidden until it can be reworked.
            - Minor Enhancements: Faster header loading, improved buttons on the Server Admin page, and various bug fixes.
            """
        ),
        new ReleaseVersion(
            "0.3.0",
            "2025-03-19",
            "Official third beta version",
            """
            - Translations: The website is now available in multiple languages via AI-powered translations. You can help improve them by contributing on GitHub!
            - Margin Calculation: Apply margin-based pricing between skills to ensure fairer trade between producers and buyers.
            - Default Prices: Server admins can now define default prices for items. These are automatically applied and can be viewed by users.
            """
        ),
        new ReleaseVersion(
            "0.2.0",
            "2024-12-09",
            "Official second beta version",
            """
            - UI Improvements: Enhanced interface for better readability and overall user experience.
            - Eco Sync: Sync your in-game prices using a chat command from the [EcoGnomeMod](https://github.com/Eco-Gnome/eco-gnome-mod).
            - Translation Support: The website is now structured to support all languages available in Eco (translations are still in progress).
            - Margin Options: Choose between two margin calculation modes: Markup or Gross Margin.
            """
        ),
        new ReleaseVersion(
            "0.1.0",
            "2024-11-18",
            "Official first beta version",
            """
            - Price Calculator: Compute item prices with advanced behavior and custom rules.
            - Graph View: Visualize your production chains as interactive graphs.
            - User Management: Save and load your configuration online with simple account handling.
            - Server Management: Export your server data using the [EcoGnomeMod](https://github.com/Eco-Gnome/eco-gnome-mod), and import custom recipes and settings.
            """
        ),

    ];
}
