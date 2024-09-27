using Microsoft.AspNetCore.Authorization;

namespace ecocraft.Services;

public class Authorization(ContextService contextService) : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (contextService.CurrentUser is not null && contextService.CurrentUserServer is not null)
        {
            var pendingRequirements = context.PendingRequirements.ToList();

            foreach (var requirement in pendingRequirements)
            {
                if (requirement is IsServerAdminRequirement)
                {
                    if (contextService.CurrentUserServer.IsAdmin) 
                    {
                        context.Succeed(requirement);
                    }
                }
            }
        }
        
        return Task.CompletedTask;
    }
}

public class IsServerAdminRequirement : IAuthorizationRequirement;
