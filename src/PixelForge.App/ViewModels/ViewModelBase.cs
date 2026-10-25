using CommunityToolkit.Mvvm.ComponentModel;
using PixelForge.Core.Localization;

namespace PixelForge.App.ViewModels;

/// <summary>
/// Abstract base class for all ViewModels in the application.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Centralized dynamic localization service instance for XAML bindings: {Binding Loc[Key]}
    /// </summary>
    public LocalizationService Loc => LocalizationService.Instance;

    protected ViewModelBase()
    {
        LocalizationService.Instance.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Loc));
    }
}
