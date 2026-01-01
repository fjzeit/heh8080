using CommunityToolkit.Mvvm.ComponentModel;

namespace Heh8080.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}
