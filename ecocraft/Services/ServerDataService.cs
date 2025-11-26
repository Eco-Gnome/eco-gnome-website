using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class ServerDataService(
    IDbContextFactory<EcoCraftDbContext> factory,
    ServerDbService serverDbService,
    UserServerDbService userServerDbService,
    ItemOrTagDbService itemOrTagDbService)
{
    public async Task CopyServerContribution(Server copyServer)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            var data = await itemOrTagDbService.GetByServerAsync(copyServer, context);

            foreach (var item in copyServer.ItemOrTags)
            {
                var copyItem = data.FirstOrDefault(i => i.Name == item.Name);

                if (copyItem is not null)
                {
                    item.MinPrice = copyItem.MinPrice;
                    item.DefaultPrice = copyItem.DefaultPrice;
                    item.MaxPrice = copyItem.MaxPrice;
                }
            }

            await context.SaveChangesAsync();
        });
    }

    public async Task Dissociate(Server server)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            server.EcoServerId = null;
            serverDbService.UpdateEcoServerId(context, server);

            foreach (var userServer in server.UserServers)
            {
                userServer.EcoUserId = null;
                userServer.Pseudo = null;
                userServerDbService.UpdateEcoUserIdAndPseudo(context, userServer);
            }

            return Task.CompletedTask;
        });
    }
}
