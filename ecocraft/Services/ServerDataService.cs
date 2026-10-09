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

            // Plus de serveur Eco à interroger pour les prix du marché.
            await context.Servers.Where(s => s.Id == server.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.EcoWebUrl, (string?)null)
                .SetProperty(x => x.EcoWebSecretProtected, (string?)null));
            server.EcoWebUrl = null;

            // Le serveur Eco et tous ses joueurs perdent leurs jetons d'API.
            await context.EcoApiTokens
                .Where(t => t.ServerId == server.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow));
        });
    }
}
