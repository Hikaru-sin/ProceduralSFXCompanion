using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProceduralSFXCompanion.Models;

namespace ProceduralSFXCompanion.Services;

public class AppSettingsService
{
    private readonly Lock _lock = new();

    private bool _isDirty;    
    private readonly string _filePath;
    public AppSettings AppSettings { get; }
    public string SettingsFolderPath { get; }
    
    public AppSettingsService()
    {
        SettingsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProceduralSFXCompanion");
        if(!Directory.Exists(SettingsFolderPath))
            Directory.CreateDirectory(SettingsFolderPath);
        
        _filePath = Path.Combine(SettingsFolderPath, "AppSettings.json");
        if (File.Exists(_filePath))
        {
            string json = File.ReadAllText(_filePath);
            AppSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            foreach (var folder in AppSettings.FolderPaths)
            {
                folder.Value.Path = folder.Key;
                folder.Value.IsRemovable = true;
            }
            foreach (var folder in AppSettings.EditingFolderPaths)
            {
                folder.Value.Path = folder.Key;
                folder.Value.IsRemovable = true;
            }
        }
        else
        {
            AppSettings = new AppSettings();
        }
    }

    public void MarkAsModified()
    {
        _isDirty = true;
    }
    
    public void TrySave(out Exception? ex)
    {
        ex = null;
        if(!_isDirty)
            return;
        
        try
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(AppSettings, options);
                if (File.Exists(_filePath))
                {
                    var tempFilePath = _filePath + ".temp";
                    File.WriteAllText(tempFilePath, json);
                    File.Replace(tempFilePath, _filePath, null);
                }
                else
                {
                    File.WriteAllText(_filePath, json);
                }
                _isDirty = false;
            }
        }
        catch (Exception e)
        {
            ex = e;
        }
    }
}