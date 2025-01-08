using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using SamplePlugin.Windows;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;


namespace SamplePlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        // Dalamud Services
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        public string Name => "YT Import for Yue's Dj";

        private const string CommandName = "/ytimport";

        public Configuration Configuration { get; init; }

        private readonly WindowSystem windowSystem = new("YT Import for Yue's Dj");

        private ConfigWindow configWindow;
        private MainWindow mainWindow;

        public Plugin()
        {
            // 1) Config laden (oder neu)
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ResourceChecker.CheckResources();

            // 2) Tools entpacken (yt-dlp, ffmpeg)
            ToolLoader.ExtractTools();

            ECommonsMain.Init(PluginInterface, this);

            // 3) GUI-Fenster anlegen
            configWindow = new ConfigWindow(this);
            mainWindow = new MainWindow(this);

            windowSystem.AddWindow(configWindow);
            windowSystem.AddWindow(mainWindow);

            // 4) Slash-Command
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the YT Import for Yue's Dj main window."
            });

            // 5) UI-Callbacks
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

            Log.Information("=== YT Import for Yue's Dj is loaded ===");
        }

        public void Dispose()
        {
            // Fenster entfernen
            windowSystem.RemoveAllWindows();

            // Fenster disposen
            configWindow?.Dispose();
            mainWindow?.Dispose();

            // Command entfernen
            CommandManager.RemoveHandler(CommandName);

            // Config speichern
            Configuration.Save();
        }

        private void OnCommand(string command, string args)
        {
            // Toggle Main Window
            ToggleMainUI();
        }

        public void RefreshMods()
        {
            Svc.Framework.RunOnTick(() =>
            {
                Chat.Instance.SendMessage("/penumbra reload mods");
            });
        }

        private void DrawUI() => windowSystem.Draw();

        public void ToggleConfigUI() => configWindow.Toggle();
        public void ToggleMainUI() => mainWindow.Toggle();
    }
}
