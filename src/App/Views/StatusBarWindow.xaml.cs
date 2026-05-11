using System.Windows;
using System.Windows.Input;
using StepsRecorder.App.ViewModels;

namespace StepsRecorder.App.Views;

public partial class StatusBarWindow : Window
{
    public StatusBarViewModel ViewModel => (StatusBarViewModel)DataContext;

    public StatusBarWindow()
    {
        InitializeComponent();
        PositionBottomRight();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 20;
        Top = area.Bottom - Height - 20;
    }
}
