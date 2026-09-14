using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using DeckUPipes.App.Services;

namespace DeckUPipes.App.UI;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        AboutLogo.Source = LogoService.GetBitmapSource();
        AboutVersion.Text = $"Version {AppInfo.Version}";
        Icon = LogoService.GetBitmapSource();
    }

    private void OnKoFiNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}

