using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Notch.Native;

/// <summary>Discos locais e removíveis com espaço usado (aba "Armazenamento").</summary>
internal static class DriveScanner
{
    /// <summary>
    /// Lê fora da thread de UI: um pendrive ou leitor de cartão lento pode demorar a responder. Unidades de rede e de
    /// CD/DVD ficam de fora (podem travar por segundos). Não lança.
    /// </summary>
    public static Task<List<DriveUsage>> ScanAsync(ILogger logger) => Task.Run(() =>
    {
        var drives = new List<DriveUsage>();
        DriveInfo[] all;
        try
        {
            all = DriveInfo.GetDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Não foi possível listar os discos");
            return drives;
        }

        foreach (var drive in all)
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
            {
                continue;
            }

            try
            {
                if (!drive.IsReady || drive.TotalSize <= 0)
                {
                    continue;
                }

                drives.Add(new DriveUsage(
                    drive.RootDirectory.FullName,
                    drive.VolumeLabel,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    drive.DriveType == DriveType.Removable));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Removido no meio da leitura, BitLocker bloqueado...: só esse disco fica de fora.
                logger.LogDebug(ex, "Não foi possível ler o disco {Drive}", drive.Name);
            }
        }

        return drives;
    });
}
