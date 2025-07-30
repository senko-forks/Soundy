using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Automation;
using ECommons.DalamudServices;
using System;
using System.IO.Pipelines;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;


namespace Soundy;

internal enum ReloadResult { Success = 0, Failure = 1 }   // 0 = OK

internal sealed class PenumbraApi
{
    private readonly Func<string, int> _reloadMod;
    public PenumbraApi()
    {
        _reloadMod = Svc.PluginInterface
            .GetIpcSubscriber<string, int>("Penumbra.ReloadMod")
            .InvokeFunc;
    }

    public void ReloadMod(string modDir)
    {
        if (string.IsNullOrWhiteSpace(modDir))
        {
            ReloadAllMods();
            return;
        }

        Svc.Framework.RunOnTick(() =>
        {
            try
            {
                var rc = (ReloadResult)_reloadMod(modDir);
                Svc.Chat.Print($"[Penumbra] ReloadMod \"{modDir}\" → {rc}");

                if (rc != ReloadResult.Success)
                    ReloadAllMods();
            }
            catch (IpcNotReadyError ex)
            {
                Svc.Chat.PrintError($"Penumbra IPC nicht bereit: {ex.Message}");
                ReloadAllMods();
            }
            catch (IpcError ex)
            {
                Svc.Chat.PrintError($"Penumbra IPC‑Fehler: {ex.Message}");
                ReloadAllMods();
            }
            catch (Exception ex)
            {
                Svc.Chat.PrintError($"Unerwarteter Fehler: {ex}");
                ReloadAllMods();
            }
        });
    }

    public void ReloadAllMods()
    {
        try
        {
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/penumbra reload");
            });
        }
        catch (Exception)
        {
        }
    }
}
