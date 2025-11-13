using System;
using System.Windows;
using FileSignatureChecker.ViewModels;

namespace FileSignatureChecker;

/// <summary>
/// Code-behind для SchemaValidationView
/// В MVVM подходе здесь минимум кода - только то, что касается UI
/// </summary>

public partial class SchemaValidationView : Window
{
    public SchemaValidationView()
    {
        InitializeComponent();
        
        if (DataContext is SchemaValidationViewModel viewModel)
        {
            viewModel.CloseRequested += OnCloseRequested;
        }
    }
    
    /// <summary>
    /// Обработчик события закрытия из ViewModel.
    /// ViewModel не может напрямую закрыть окно (это нарушило бы MVVM),
    /// поэтому он вызывает событие, а View его обрабатывает
    /// </summary>
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        this.Close();
    }
    
    /// <summary>
    /// Вызывается при закрытии окна
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is SchemaValidationViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }

        base.OnClosed(e);
     
        Application.Current.MainWindow?.Show();
    }
}