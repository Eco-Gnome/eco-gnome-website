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
            "1.3.5",
            "2026-06-27",
            "Production Chain Graph, Server Data Editor & Shopping List Improvements",
            """
            - Production Chain Graph: New "Visualize Production Chain" view in the Shopping List that lays out your full crafting chain as an interactive diagram, making it easy to follow how items flow from raw resources to finished products.
            - Server Data Editor: New admin page (/data-editor) to manually create and edit server data — Items & Tags, Recipes (simplified, without modifiers), Skills, Crafting Tables and Plugin Modules — with safe-delete guards and cascade handling.
            - Recipe Output Lock (Admin): Server admins can lock a recipe's output share distribution and clamp item min/max prices, keeping calculated prices within agreed bounds.
            - Shopping List – Recipe Preview: The "add recipe" dropdown and the recipe-choice tooltips now show a compact recipe summary (product icon plus ingredient icons with quantities), making it easy to tell apart variants of the same item.
            - Shopping List – Quantity Meaning: The per-recipe quantity now means "number of primary-product items to produce". Craft counts round up to whole crafts, so you always produce at least the requested amount.
            - Super Admin: Reworked the super-admin dashboard layout and filters, and added a per-server detail view.
            - Fixes: data editor name-field handling, and various stability improvements.
            """
        ),
        new ReleaseVersion(
            "1.3.4",
            "2026-06-07",
            "Crafting Table Fuel Costs, Super Admin Improvements & Fixes",
            """
            - Crafting Table Fuel: Crafting tables that burn fuel now derive their cost per minute from the fuel itself. Pick the fuel item used on each table and Eco Gnome computes the cost from the table's fuel consumption and the fuel's calories, instead of a fixed manual value.
            - Fuel Selection: Eligible fuels are listed per crafting table and grouped by their accepted fuel tag, with the cheapest fuel automatically used for a group. The matching fuel items are surfaced in the Price Calculator so their prices feed into crafting costs.
            - Additional Fee: You can still add a manual fee on top of the fuel cost; the total cost per minute combines both.
            - Super Admin: Improved the super admin dashboard layout and filters, and added a per-server detail view.
            - Fixes: Fixed Price Calculator table search, fuel element alignment in the recipe dialog, fuel grouping edge cases, and several data hydration and persistence issues.
            """
        ),
        new ReleaseVersion(
            "1.3.3",
            "2026-05-05",
            "Price Calculator UX Overhaul, Smart Rounding & Blueprint Awareness",
            """
            - Items-to-Sell Collapse: To improve performance on large datasets, skill groups beyond the third are now collapsed by default. Searching auto-expands all groups when results are narrow and restores the previous layout when the search is cleared.
            - Recipe Families: Items belonging to the same recipe family (e.g. the wood variants of Composite Lumber) are now sorted side-by-side within their skill group, with a subtle visual grouping (no internal separator, tighter padding) to help scanning.
            - Zebra Striping: Added alternating row backgrounds on Skills, Crafting Tables, Margins, Items-to-Buy and Items-to-Sell tables, with families counted as a single block so neighbouring families read as distinct units.
            - Blueprint Icon: Blueprint recipes are now flagged with a small icon next to their name in items-to-sell rows, the recipe dialog title, and the "Produced by"/"Used in" lists.
            - Hide Blueprints Option: New checkbox in the Options panel to filter blueprint recipes out of the items-to-sell display.
            - Smart Margin Rounding: Each user margin can be configured with a Rounding mode (None / Round up / Round down / Marketing) that snaps margin prices to clean values for in-store display. Steps: $1-$2 → 0.25, $2-$5 → 0.5, $5-$100 → 1, $100-$1000 → 10, $1000+ → 100. Marketing applies the classic ".99" trick on the upper tiers.
            - Calculate Optimisation: Reduced price calculation time on large data contexts.
            - Stability: Fixed a deletion regression during data import, hardened talent yield handling, and hydrated server members on initial context load.
            """
        ),
        new ReleaseVersion(
            "1.3.2",
            "2026-04-25",
            "Economy Viewer, Player Drill-down & Reliability Improvements",
            """
            - Economy Viewer: Added a new read-only Economy Viewer page to analyze server-wide economy data without changing player or server settings.
            - Global Analytics: Added aggregated views by Item, Recipe, Skill, and Player with search, sortable min/avg/max price and margin metrics, configured players/contexts, and spread analysis.
            - Deeper Visibility: Added expandable rows in global tables to inspect per-player context details and quickly identify where values diverge.
            - Player Drill-down: Added side-by-side comparison between two players with context selection, Item/Recipe/Skill comparison modes, delta indicators, and clear status badges.
            """
        ),
        new ReleaseVersion(
            "1.3.1",
            "2026-04-17",
            "MudBlazor 9.3 Migration, Shopping List Guided Expansion & Stability Improvements",
            """
            - UI Framework Upgrade: Migrated Eco Gnome to MudBlazor 9.3 and aligned component behavior.
            - Shopping List Guided Expansion: Added guided auto-expand to raw resources with pause on ambiguous recipe choices, automatic resume after selection, inline stop control, and optional propagation of recipe choices across identical ingredients.
            - Shopping List UX: Added auto-scroll to the current pending choice, immediate node ordering without refresh, and horizontal scrolling to keep recipe rows readable on one line.
            - Shopping List Reliability: Improved recursion depth handling, by-product exclusion (`!DefaultIsReintegrated`), branch synchronization, and manual sub-recipe quantity consistency.
            - Price Calculator & Policies: Improved calorie/margin policy handling, enforced safer numeric input behavior, and reduced talent recalculation noise with debouncing.
            - Server/Admin/API Fixes: Resolved multiple regressions around tag APIs, price persistence, server management refresh, and admin/server edge cases.
            - Deployment & Tooling: Improved Docker and DataMigrator integration, with CI/CD and migration reliability fixes.
            - Stability: Fixed several EF tracking/deletion edge cases and hardened calculation error handling.
            """
        ),
        new ReleaseVersion(
            "1.3.0",
            "2026-04-08",
            "PostgreSQL Migration, Shopping List Improvements & Better Talent Handling",
            """
            - Eco v13 Compatibility: Eco Gnome is now compatible with version 13 of Eco.
            - PostgreSQL Migration: Eco Gnome now runs on PostgreSQL, with updated data access, migrations, and server-side persistence improvements.
            - Deployment & Migration Tools: Added Docker support and a dedicated data migration tool to make hosting and moving existing data easier.
            - Shopping List Improvements: The Shopping List has been significantly improved, with better recipe tree handling, more reliable parent/child tracking, and more accurate crafting requirement calculations.
            - Dynamic Quantity Updates: Shopping List sub-recipes now react properly to talent, skill, and module changes so required craft counts stay in sync.
            - Talent Improvements: Added support for multi-level talents and improved talent import/retrieval behavior.
            - Stability Fixes: Fixed several PostgreSQL-specific issues, including cascade deletion problems, shopping list persistence issues, and duplicate/incorrect user-server data behavior.
            """
        ),
        new ReleaseVersion(
            "1.2.1",
            "2025-06-14",
            "Pre release of Shopping List",
            """
            - Shopping List: Add a new feature in development, the Shopping List. Give us your feedback so we can improve it!
            """
        ),
        new ReleaseVersion(
            "1.2.0",
            "2025-05-13",
            "Usability Improvements & Bug Fixes",
            """
            - Sharing improvements: Auto-balancing can now be disabled, giving you full control over shared percentages.
            - Eco API integration: Added support for shop category/item creation and precise data context targeting in EcoGnomeMod.
            - Data context fix: Resolved data leakage between contexts, which caused incorrect price calculations after skill removal.
            - Server copy fix: Ingredient data now correctly copied, ensuring proper price calculations.
            - Various minor fixes enhancing overall usability.
            """
        ),
        new ReleaseVersion(
            "1.1.2",
            "2025-04-30",
            "Performance improvement",
            """
            - UX: Greatly reduced loading time of server & user data. Added smarter loader when switching between servers.
            """
        ),
        new ReleaseVersion(
            "1.1.1",
            "2025-04-28",
            "Private modded icons",
            """
            - Modded Icons: Add the ability to upload private mod icons, so only your server can use them. You can also overwrite official icons if needed. Note: you still need to be a mod upload user. If you’d like to become one, feel free to contact us!
            """
        ),
        new ReleaseVersion(
            "1.1.0",
            "2025-04-23",
            "Tabs & Modded Icons",
            """
            - Price Calculator Tabs: You can now create multiple Price Calculator configurations using tabs. Only the default tab can be synced with the game. Let us know how you use them and how we could make this feature even better!
            - Modded Icons: Super Admins and selected key community members can now upload custom modded icons. These icons are shared across all servers. Want to contribute? Reach out to us!
            - Navigation: Username and language now load faster when opening pages. We’re also working on improving overall server loading speed.
            - Technical: Upgraded to MudBlazor 8.x and .NET 9.x. EcoGnome now handles reconnections much more smoothly if the connection drops.
            """
        ),
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
