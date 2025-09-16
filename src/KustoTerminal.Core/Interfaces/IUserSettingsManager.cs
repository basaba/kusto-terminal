using System.Threading.Tasks;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces
{
    public interface IUserSettingsManager
    {
        Task<UserSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(UserSettings settings);
        Task SaveLastQueryAsync(string query);
        Task<string> GetLastQueryAsync();
    }
}