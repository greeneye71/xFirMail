// SPDX-License-Identifier: EUPL-1.2
using System.Windows;

namespace Xfir.App;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();
        WindowPlacement.ConstrainDialog(this);
        DisclaimerText.Text = AppInfo.Disclaimer;
    }

    private void OnAcceptChanged(object sender, RoutedEventArgs e) => AcceptButton.IsEnabled = AcceptCheck.IsChecked == true;

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnDecline(object sender, RoutedEventArgs e) => DialogResult = false;
}
