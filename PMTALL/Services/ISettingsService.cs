using PMTALL.Models;

namespace PMTALL.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
