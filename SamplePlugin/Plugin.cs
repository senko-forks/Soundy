using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using Soundy.Windows;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace Soundy
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
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

        public string Name => "Soundy";

        private const string CommandName = "/soundy";

        public Configuration Configuration { get; init; }

        private readonly WindowSystem windowSystem = new("Soundy for Yue's DJ");

        private ConfigWindow configWindow;
        private MainWindow mainWindow;

        public Plugin()
        {
            // 1) Load or create configuration
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Save(); // Ensure the configuration is saved

            ResourceChecker.CheckResources();

            // 2) Tools are now downloaded via GUI, so remove ExtractTools() call
            // ToolLoader.ExtractTools();

            ECommonsMain.Init(PluginInterface, this);

            // 3) Initialize GUI windows
            configWindow = new ConfigWindow(this);
            mainWindow = new MainWindow(this);

            windowSystem.AddWindow(configWindow);
            windowSystem.AddWindow(mainWindow);

            // 4) Register Slash Command
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Soundy's main window."
            });

            // 5) Register UI callbacks
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

            Log.Information("=== Soundy loaded ===");

        }

        public void Dispose()
        {
            // Remove windows
            windowSystem.RemoveAllWindows();

            // Dispose windows
            configWindow?.Dispose();
            mainWindow?.Dispose();

            // Remove command handler
            CommandManager.RemoveHandler(CommandName);

            // Save configuration
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

        public void SendError(string message)
        {
            ChatGui.PrintError(message);
        }

        private void DrawUI() => windowSystem.Draw();

        public void ToggleConfigUI() => configWindow.Toggle();

        public void ToggleMainUI() => mainWindow.Toggle();

    }
}
