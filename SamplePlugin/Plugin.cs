using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.IO;
using YTImport.Windows;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace YTImport
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

        public string Name => "YTImport";

        private const string CommandName = "/ytimport";

        public Configuration Configuration { get; init; }

        private readonly WindowSystem windowSystem = new("YT Import for Yue's DJ");

        private ConfigWindow configWindow;
        private MainWindow mainWindow;
        private DeleteWindow deleteWindow; // Add DeleteWindow

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
            deleteWindow = new DeleteWindow(this); // Initialize DeleteWindow

            windowSystem.AddWindow(configWindow);
            windowSystem.AddWindow(mainWindow);
            windowSystem.AddWindow(deleteWindow); // Add DeleteWindow to WindowSystem

            // 4) Register Slash Command
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the YT Import for Yue's DJ main window."
            });

            // 5) Register UI callbacks
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

            Log.Information("=== YT Import for Yue's DJ loaded ===");
        }

        public void Dispose()
        {
            // Remove windows
            windowSystem.RemoveAllWindows();

            // Dispose windows
            configWindow?.Dispose();
            mainWindow?.Dispose();
            deleteWindow?.Dispose(); // Dispose DeleteWindow

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

        private void DrawUI() => windowSystem.Draw();

        public void ToggleConfigUI() => configWindow.Toggle();

        public void ToggleMainUI() => mainWindow.Toggle();

        // Method to toggle DeleteWindow (optional: you can add a separate command or button to open it)
        public void ToggleDeleteUI()
        {
            deleteWindow.Toggle();
        }
    }
}
