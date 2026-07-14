using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DicomClassifier.ViewModels;

namespace DicomClassifier;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AllowDrop = true;
        SetupDragDrop();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveSettings();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon == null) return;
        if (WindowState == WindowState.Maximized)
        {
            MaximizeIcon.Data = Geometry.Parse("M1,4 L7,4 L7,10 L1,10 Z M5,1 L11,1 L11,9 L5,9 Z");
            MaximizeBtn.ToolTip = "Restore Down";
        }
        else
        {
            MaximizeIcon.Data = Geometry.Parse("M2,2 L10,2 L10,10 L2,10 Z");
            MaximizeBtn.ToolTip = "Maximize";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // RawToDicom slice range helpers
    private void RawSliceAll_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RawSliceStart = 0;
            vm.RawSliceEnd = vm.RawSliceMax;
        }
    }

    private void RawSliceCenter50_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var center = vm.RawPreviewSliceIndex;
            vm.RawSliceStart = Math.Max(0, center - 50);
            vm.RawSliceEnd = Math.Min(vm.RawSliceMax, center + 50);
        }
    }

    private void RawSliceCenter100_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var center = vm.RawPreviewSliceIndex;
            vm.RawSliceStart = Math.Max(0, center - 100);
            vm.RawSliceEnd = Math.Min(vm.RawSliceMax, center + 100);
        }
    }

    // Tab navigation
    private void SortTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 0;
    }

    private void ClassifyTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 1;
    }

    private void StatsTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 2;
    }

    private void DoseFixTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 3;
    }

    private void RawToDicomTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.SelectedTabIndex = 4;
    }

    // RawToDicom drag-drop handlers
    private void RawFileDropBorder_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            var path = paths[0];
            if (DataContext is MainViewModel vm)
            {
                vm.SetSourceFolderFromDrop(path);
            }
        }
        RawFileDropBorder.BorderBrush = FindResource("BorderBrush") as SolidColorBrush;
        RawFileDropBorder.Background = FindResource("SurfaceBrush") as SolidColorBrush;
    }

    private void RawFileDropBorder_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            RawFileDropBorder.BorderBrush = FindResource("AccentBrush") as SolidColorBrush;
            RawFileDropBorder.Background = FindResource("SurfaceLightBrush") as SolidColorBrush;
        }
    }

    private void RawFileDropBorder_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        RawFileDropBorder.BorderBrush = FindResource("BorderBrush") as SolidColorBrush;
        RawFileDropBorder.Background = FindResource("SurfaceBrush") as SolidColorBrush;
    }

    // Drag-drop support routed to whichever tab is active
    private void SetupDragDrop()
    {
        DropBorder.Drop += OnDrop;
        DropBorder.DragOver += OnDragOver;
        DropBorder.DragLeave += OnDragLeave;
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            var path = paths[0];
            if (DataContext is MainViewModel vm)
            {
                if (System.IO.File.Exists(path))
                {
                    vm.SetSourceFolderFromDrop(path);
                }
                else if (System.IO.Directory.Exists(path))
                {
                    vm.SetSourceFolderFromDrop(path);
                }
            }
        }
        DropBorder.BorderBrush = FindResource("BorderBrush") as System.Windows.Media.SolidColorBrush;
        DropBorder.Background = FindResource("SurfaceBrush") as System.Windows.Media.SolidColorBrush;
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            DropBorder.BorderBrush = FindResource("AccentBrush") as System.Windows.Media.SolidColorBrush;
            DropBorder.Background = FindResource("SurfaceLightBrush") as System.Windows.Media.SolidColorBrush;
        }
    }

    private void OnDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        DropBorder.BorderBrush = FindResource("BorderBrush") as System.Windows.Media.SolidColorBrush;
        DropBorder.Background = FindResource("SurfaceBrush") as System.Windows.Media.SolidColorBrush;
    }
}
