using ecocraft.Models;

namespace ecocraft.Services;

public interface IPriceCalculatorService
{
    (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTags(DataContext dataContext);
    (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTagsForDisplay(DataContext dataContext);
    Task Calculate(DataContext dataContext, string triggerOrigin = "Unknown");
}
