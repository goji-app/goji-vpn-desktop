using System.Windows.Controls;

namespace GodjiVpn.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>Журнал открывается на последних строках — свежие события внизу, как в
    /// LogViewerDialog (Android).</summary>
    private void OnLogTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.ScrollToEnd();
    }
}
