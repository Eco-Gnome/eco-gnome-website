using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Résultat de BuildingGenerator.Generate : Document null ⇔ Error renseigné (validation en échec ou programme vide) ;
// Notes = ajustements faits en silence (clamps, matériaux remplacés, éléments abandonnés), à montrer à l'utilisateur.
public sealed record GeneratorResult(PlanDocument? Document, List<string> Notes, string? Error);
