using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ShoppingListSkillDbService(EcoCraftDbContext context)
{
    public ShoppingListSkill Add(ShoppingListSkill shoppingListSkill)
    {
        context.ShoppingListSkills.Add(shoppingListSkill);
        return shoppingListSkill;
    }

    public void Update(ShoppingListSkill shoppingListSkill)
    {
        context.ShoppingListSkills.Update(shoppingListSkill);
    }

    public void Delete(ShoppingListSkill shoppingListSkill)
    {
        context.ShoppingListSkills.Remove(shoppingListSkill);
    }
}
