using DicomClassifier.Models;

namespace DicomClassifier.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
