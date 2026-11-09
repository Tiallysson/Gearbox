using Microsoft.Extensions.Logging;
using Windows.Networking.Connectivity;

namespace Gearbox.Modules.Notch.Native;

internal static class ConnectionCost
{
    public static bool IsMetered(ILogger logger)
    {
        try
        {
            var cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
            return cost is not null &&
                (cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable ||
                 cost.Roaming ||
                 cost.OverDataLimit ||
                 cost.ApproachingDataLimit);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Não foi possível saber se a conexão é limitada");
            return false;
        }
    }
}
