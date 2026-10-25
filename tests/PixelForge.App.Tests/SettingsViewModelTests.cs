using System.Xml.Linq;
using FluentAssertions;
using PixelForge.App.ViewModels;
using PixelForge.Core.Localization;
using PixelForge.Core.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void SettingsViewModel_Initialization_ShouldPopulateFromAppSettings()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.DefaultCanvasWidth = 48;
            service.MaxUndoSteps = 150;
            service.Language = AppLanguage.English;

            var vm = new SettingsViewModel(service);

            vm.DefaultCanvasWidth.Should().Be(48);
            vm.MaxUndoSteps.Should().Be(150);
            vm.SelectedLanguage.Should().Be(AppLanguage.English);
            vm.SelectedTabIndex.Should().Be(0);
            vm.IsTab0.Should().BeTrue();
            vm.IsTab1.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsViewModel_TabSelection_ShouldUpdateTabIndicesAndFlags()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            var vm = new SettingsViewModel(service);

            vm.SelectTab(2);
            vm.SelectedTabIndex.Should().Be(2);
            vm.IsTab2.Should().BeTrue();
            vm.IsTab0.Should().BeFalse();

            vm.SelectTab(5);
            vm.SelectedTabIndex.Should().Be(5);
            vm.IsTab5.Should().BeTrue();
            vm.IsTab2.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsViewModel_ApplyCanvasPreset_ShouldUpdateDimensions()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            var vm = new SettingsViewModel(service);

            var preset = new CanvasPresetItem(64, 64, "64 × 64");
            vm.ApplyCanvasPreset(preset);

            vm.DefaultCanvasWidth.Should().Be(64);
            vm.DefaultCanvasHeight.Should().Be(64);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsViewModel_SaveAndApply_ShouldPersistAndRaiseEvents()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            var vm = new SettingsViewModel(service);

            bool appliedFired = false;
            bool closeFired = false;
            vm.SettingsApplied += (_, _) => appliedFired = true;
            vm.RequestClose += (_, _) => closeFired = true;

            vm.DefaultCanvasWidth = 128;
            vm.DefaultCanvasHeight = 128;
            vm.DefaultStabilizerStrength = 7;
            vm.DefaultPixelPerfect = true;
            vm.AutosaveIntervalMinutes = 15;
            vm.MaxUndoSteps = 220;

            vm.SaveAndApply();

            appliedFired.Should().BeTrue();
            closeFired.Should().BeTrue();

            service.DefaultCanvasWidth.Should().Be(128);
            service.DefaultCanvasHeight.Should().Be(128);
            service.DefaultStabilizerStrength.Should().Be(7);
            service.DefaultPixelPerfect.Should().BeTrue();
            service.AutosaveIntervalMinutes.Should().Be(15);
            service.MaxUndoSteps.Should().Be(220);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsViewModel_ResetToDefaults_ShouldRestoreFactoryDefaultsInVM()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            var vm = new SettingsViewModel(service)
            {
                DefaultCanvasWidth = 512,
                MaxUndoSteps = 400,
                IsDarkTheme = false
            };

            vm.ResetToDefaults();

            vm.DefaultCanvasWidth.Should().Be(32);
            vm.MaxUndoSteps.Should().Be(100);
            vm.IsDarkTheme.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsViewModel_Cancel_ShouldRaiseRequestCloseWithoutSaving()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new AppSettingsService(tempFile);
            service.DefaultCanvasWidth = 32;

            var vm = new SettingsViewModel(service);
            bool closeFired = false;
            vm.RequestClose += (_, _) => closeFired = true;

            vm.DefaultCanvasWidth = 1024;
            vm.Cancel();

            closeFired.Should().BeTrue();
            service.DefaultCanvasWidth.Should().Be(32);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SettingsWindow_XAML_ShouldDeclareAllRequiredControlsAndTabs()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../src/PixelForge.App/Views/SettingsWindow.axaml");
        if (!File.Exists(xamlPath))
        {
            xamlPath = Path.GetFullPath("src/PixelForge.App/Views/SettingsWindow.axaml");
        }

        File.Exists(xamlPath).Should().BeTrue();

        var xml = XDocument.Load(xamlPath);
        var xmlStr = xml.ToString();

        // Check tabs and headers
        xmlStr.Should().Contain("Dlg_SettingsTab_General");
        xmlStr.Should().Contain("Dlg_SettingsTab_Canvas");
        xmlStr.Should().Contain("Dlg_SettingsTab_Tools");
        xmlStr.Should().Contain("Dlg_SettingsTab_Autosave");
        xmlStr.Should().Contain("Dlg_SettingsTab_Performance");
        xmlStr.Should().Contain("Dlg_SettingsTab_Hotkeys");

        // Check action buttons
        xmlStr.Should().Contain("SaveAndApplyCommand");
        xmlStr.Should().Contain("ResetToDefaultsCommand");
        xmlStr.Should().Contain("CancelCommand");
    }

    [Fact]
    public void MainWindow_XAML_ShouldDeclareSettingsMenuItems()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../src/PixelForge.App/Views/MainWindow.axaml");
        if (!File.Exists(xamlPath))
        {
            xamlPath = Path.GetFullPath("src/PixelForge.App/Views/MainWindow.axaml");
        }

        File.Exists(xamlPath).Should().BeTrue();

        var xml = XDocument.Load(xamlPath);
        var xmlStr = xml.ToString();

        xmlStr.Should().Contain("MenuSettingsFile");
        xmlStr.Should().Contain("MenuSettingsEdit");
        xmlStr.Should().Contain("OpenSettingsCommand");
    }
}
