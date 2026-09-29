using System.Windows;
using System.Windows.Controls;
using GodjiVpn.ViewModels;

namespace GodjiVpn.Views;

public partial class FaqView : UserControl
{
    public FaqView()
    {
        InitializeComponent();
    }

    private void OnQuestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FaqItemUi item }) item.ToggleCommand.Execute(null);
    }
}
