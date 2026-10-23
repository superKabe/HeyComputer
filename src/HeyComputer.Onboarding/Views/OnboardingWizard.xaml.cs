using System.Windows;
using System.Windows.Controls;
using HeyComputer.Onboarding.ViewModels;

namespace HeyComputer.Onboarding.Views;

public partial class OnboardingWizard : UserControl
{
    public OnboardingWizard()
    {
        InitializeComponent();
    }

    private OnboardingWizardViewModel? ViewModel => DataContext as OnboardingWizardViewModel;

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && sender is PasswordBox passwordBox)
        {
            ViewModel.ApiKey = passwordBox.Password;
        }
    }

    private void Back_OnClick(object sender, RoutedEventArgs e) => ViewModel?.GoBack();

    private async void Continue_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await ViewModel.AdvanceAsync();
            if (string.IsNullOrEmpty(ViewModel.ApiKey))
            {
                ApiKeyBox.Clear();
            }
        }
        catch
        {
            // The view model exposes a sanitized user-facing status.
        }
    }
}
