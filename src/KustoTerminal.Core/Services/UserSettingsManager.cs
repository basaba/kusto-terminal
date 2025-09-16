using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services
{
    public class UserSettingsManager : IUserSettingsManager
    {
        private readonly string _settingsFilePath;
        private UserSettings? _cachedSettings;

        public UserSettingsManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "KustoTerminal");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "user-settings.json");
        }

        public async Task<UserSettings> LoadSettingsAsync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    _cachedSettings = JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
                }
                else
                {
                    _cachedSettings = new UserSettings();
                }
            }
            catch (Exception ex)
            {
                // If we can't load settings, create new ones and log the error
                _cachedSettings = new UserSettings();
                // Consider logging the error in a real application
                Console.WriteLine($"Warning: Failed to load user settings: {ex.Message}");
            }

            return _cachedSettings;
        }

        public async Task SaveSettingsAsync(UserSettings settings)
        {
            try
            {
                settings.LastModified = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                await File.WriteAllTextAsync(_settingsFilePath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save user settings: {ex.Message}", ex);
            }
        }

        public async Task SaveLastQueryAsync(string query)
        {
            var settings = await LoadSettingsAsync();
            settings.LastQuery = query ?? string.Empty;
            await SaveSettingsAsync(settings);
        }

        public async Task<string> GetLastQueryAsync()
        {
            var settings = await LoadSettingsAsync();
            return settings.LastQuery ?? string.Empty;
        }
    }
}