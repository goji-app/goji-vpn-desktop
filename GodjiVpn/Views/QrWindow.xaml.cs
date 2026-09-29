using System.Windows;
using GodjiVpn.Utils;

namespace GodjiVpn.Views;

/// <summary>Окно с QR-кодом текста (см. QrWindow.xaml).</summary>
public partial class QrWindow : Window
{
    public QrWindow(string title, string subtitle, string content, string? caption = null)
    {
        InitializeComponent();
        Title = title;
        TitleBlock.Text = title;
        SubtitleBlock.Text = subtitle;
        QrImage.Source = QrCode.ToImage(QrCode.Encode(content), quietZone: 1);
        CaptionBlock.Text = caption ?? "";
        CaptionBlock.Visibility = string.IsNullOrEmpty(caption) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
