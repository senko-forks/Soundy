using ECommons.Automation;
using ECommons.DalamudServices;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;     // Wrapper‑Klassen
using Soundy;
using System;
using System.IO;

internal sealed class PenumbraBridge
{
    private readonly ReloadMod reloadMod;
    private readonly EventSubscriber initializedSubscriber;

    public PenumbraBridge()
    {
        reloadMod = new ReloadMod(Svc.PluginInterface);
        initializedSubscriber = Initialized.Subscriber(Svc.PluginInterface);
    }

    public void Reload(string name)
    {
        // Erst feuern, wenn Penumbra sich meldet
        if (!IsInitialized() || string.IsNullOrWhiteSpace(name))
        {
            Fallback();   // globaler Reload
            return;
        }

        var rc = reloadMod.Invoke(name);               // Penumbra‑Wrapper

        if (rc is not PenumbraApiEc.Success)
            Fallback();
    }

    public static void ReloadAllMods()
    {
        try
        {
            Svc.Framework.RunOnTick(() =>
            {
                Chat.SendMessage("/penumbra reload");
            });
        }
        catch (Exception)
        {
        }
    }

    private bool IsInitialized()
    {
        // Logic to check if Penumbra is initialized using the subscriber
        // Assuming the subscriber has a method or property to check initialization
        return initializedSubscriber != null; // Replace with actual check logic
    }

    private static void Fallback() => ReloadAllMods();
}
