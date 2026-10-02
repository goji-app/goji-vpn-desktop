using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GodjiVpn.Controls;

/// <summary>
/// Масштаб интерфейса под размер окна: экраны свёрстаны на холсте постоянного размера
/// (DesignWidth × DesignHeight), а он пропорционально растягивается или сжимается под окно —
/// растянул окно, и весь интерфейс стал крупнее, без перекомпоновки и прокрутки.
/// Текст при масштабе 1:1 рисуется в режиме Display (чёткий, по пикселям), при любом другом —
/// в Ideal: Display под масштабом размывается.
/// </summary>
public sealed class FitScaler : Viewbox
{
    public const double DesignWidth = 440;
    public const double DesignHeight = 820;

    public FitScaler()
    {
        Stretch = Stretch.Uniform;
        StretchDirection = StretchDirection.Both;
        SizeChanged += (_, _) => UpdateTextMode();
    }

    /// <summary>Текущий масштаб холста.</summary>
    public double Scale => Math.Min(ActualWidth / DesignWidth, ActualHeight / DesignHeight);

    private void UpdateTextMode()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        TextOptions.SetTextFormattingMode(this,
            Math.Abs(Scale - 1) < 0.02 ? TextFormattingMode.Display : TextFormattingMode.Ideal);
    }
}
