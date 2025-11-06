using System;
using System.Windows;
using System.Windows.Input;

namespace FileSignatureChecker
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            
            this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
            
            this.StateChanged += MainWindow_StateChanged;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;
                MaximizeButton.ToolTip = "Восстановить";
            }
            else
            {
                MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
                MaximizeButton.ToolTip = "Развернуть";
            }
        }
    }
}
