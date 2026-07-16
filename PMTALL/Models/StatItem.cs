using CommunityToolkit.Mvvm.ComponentModel;

namespace PMTALL.Models;

public partial class StatItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private string _computedValue = "-";

    [ObservableProperty]
    private string _category = string.Empty;
}
