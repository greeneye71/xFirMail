// SPDX-License-Identifier: EUPL-1.2
using System.Windows;

namespace Xfir.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        WindowPlacement.ConstrainDialog(this);
        VersionLabel.Text = $"Versione {AppInfo.Version}";
        AuthorLabel.Text = AppInfo.Author;
        EmailLabel.Text = AppInfo.ContactEmail;
        WebsiteLabel.Text = AppInfo.Website;
        DisclaimerLabel.Text = AppInfo.Disclaimer;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
