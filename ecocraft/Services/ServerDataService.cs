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
    public async Task CopyServerContribution(Server sourceServer, Server targetServer)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            var sourceItems = await itemOrTagDbService.GetByServerAsync(sourceServer, context);
            var targetItems = await context.ItemOrTags
                .Where(i => i.ServerId == targetServer.Id)
                .ToListAsync();

            foreach (var targetItem in targetItems)
            {
                var sourceItem = sourceItems.FirstOrDefault(i => i.Name == targetItem.Name);

                if (sourceItem is not null)
                {
                    targetItem.MinPrice = sourceItem.MinPrice;
                    targetItem.DefaultPrice = sourceItem.DefaultPrice;
                    targetItem.MaxPrice = sourceItem.MaxPrice;
                }
            }
        });
    }

    public async Task Dissociate(Server server)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            server.EcoServerId = null;
            serverDbService.UpdateEcoServerId(context, server);

            var userServers = await context.UserServers
                .Where(us => us.ServerId == server.Id)
                .ToListAsync();

            foreach (var us in userServers)
            {
                us.EcoUserId = null;
                us.Pseudo = null;
            }

            // Update in-memory state
            foreach (var us in server.UserServers)
            {
                us.EcoUserId = null;
                us.Pseudo = null;
            }
        });
    }
}
