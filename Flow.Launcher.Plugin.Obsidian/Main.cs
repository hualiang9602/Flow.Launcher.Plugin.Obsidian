using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Obsidian.Models;
using Flow.Launcher.Plugin.Obsidian.Services.Implementations;
using Flow.Launcher.Plugin.Obsidian.Services.Interfaces;
using Flow.Launcher.Plugin.Obsidian.ViewModels;
using Flow.Launcher.Plugin.Obsidian.Views;
using File = System.IO.File;
using ContextMenuService = Flow.Launcher.Plugin.Obsidian.Services.Implementations.ContextMenuService;

namespace Flow.Launcher.Plugin.Obsidian;

public class Obsidian : IAsyncPlugin, ISettingProvider, IAsyncReloadable, IContextMenu
{
    private IContextMenu? _contextMenu;

    private IPublicAPI? _publicApi;
    private IQueryHandler? _queryHandler;
    private Settings? _settings;
    private SettingsViewModel? _settingsViewModel;

    private IVaultManager? _vaultManager;
    private ISettingWindowManager? _windowManager;

    private static readonly string[] SettingsUiAssemblyNames = ["ModernWpf", "ModernWpf.Controls"];
    private static bool _assemblyResolveHooked;

    public async Task InitAsync(PluginInitContext context)
    {
        _publicApi = context.API;
        LoadSettingsUiAssemblies(context);
        _settings = _publicApi.LoadSettingJsonStorage<Settings>();
        _vaultManager = new VaultManager(_settings);

        await _vaultManager.UpdateVaultListAsync();

        _queryHandler = new QueryService(_publicApi, _settings);
        _contextMenu = new ContextMenuService(this, _vaultManager, _settings);

        _windowManager = new SettingWindowManager(_settings);
        _settingsViewModel = new SettingsViewModel(this, _vaultManager, _windowManager);
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        if (_queryHandler is null || _vaultManager is null)
        {
            return [];
        }

        QueryData queryData = QueryData.Parse(query, _vaultManager.Vaults);

        return queryData.IsNoteCreationSearch()
            ? _queryHandler.HandleNoteCreation(queryData)
            : await _queryHandler.HandleQueryAsync(queryData, token);
    }

    public async Task ReloadDataAsync()
    {
        if (_vaultManager is null)
        {
            return;
        }

        await _vaultManager.UpdateVaultListAsync();
    }

    public List<Result> LoadContextMenus(Result selectedResult) =>
        _contextMenu is not null ? _contextMenu.LoadContextMenus(selectedResult) : [];

    public Control CreateSettingPanel() =>
        _settingsViewModel is null ? new Control() : new SettingsView(_settingsViewModel);

    // BAML inflation resolves ui: namespaces with Assembly.Load(assemblyName), which
    // bypasses the plugin's deps.json and folder. Flow 1.x serves these from its own
    // directory; Flow 2.x ships iNKORE.UI.WPF.Modern instead, so load the plugin's
    // own copies by explicit path before any settings XAML is inflated.
    private static void LoadSettingsUiAssemblies(PluginInitContext context)
    {
        string? pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
        if (pluginDirectory is null)
        {
            return;
        }

        if (!_assemblyResolveHooked)
        {
            _assemblyResolveHooked = true;
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                string path = Path.Combine(pluginDirectory, $"{new AssemblyName(args.Name).Name}.dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
        }

        foreach (string assemblyName in SettingsUiAssemblyNames)
        {
            string path = Path.Combine(pluginDirectory, $"{assemblyName}.dll");
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                Assembly.Load(new AssemblyName(assemblyName));
            }
            catch (FileNotFoundException)
            {
                Assembly.LoadFrom(path);
            }
        }
    }
}
