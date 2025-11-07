using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FileSignatureChecker
{
    public partial class MainWindow
    {
        private DispatcherTimer _dotsTimer;
        private int _dotsCount = 0;
        public MainWindow()
        {
            InitializeComponent();
            
            this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
            
            this.StateChanged += MainWindow_StateChanged;

            _dotsTimer = new DispatcherTimer();
            _dotsTimer.Interval = TimeSpan.FromMilliseconds(500);
            _dotsTimer.Tick += DotsTimer_Tick;
        }

        public void StartDotsAnimation()
        {
            _dotsCount = 0;
            _dotsTimer.Start();
        }

        private void DotsTimer_Tick(object? sender, EventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null && vm.IsChecking)
            {
                _dotsCount = (_dotsCount + 1) % 4;
                var dots = new string('.', _dotsCount);

                var baseText = vm.ProgressText.TrimEnd('.');
                vm.ProgressText = baseText + dots;
            }
            else
            {
                _dotsTimer.Stop();
                _dotsCount = 0;
            }
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
