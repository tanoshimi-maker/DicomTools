using System.Windows;
using System.Windows.Media.Animation;

namespace DicomClassifier;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Update the status message shown below the progress bar.
    /// </summary>
    public void SetStatus(string message)
    {
        if (Dispatcher.CheckAccess())
            StatusText.Text = message;
        else
            Dispatcher.Invoke(() => StatusText.Text = message);
    }

    /// <summary>
    /// Fade out and close the splash window.
    /// </summary>
    public async Task FadeOutAndCloseAsync(TimeSpan? duration = null)
    {
        var fadeDuration = duration ?? TimeSpan.FromMilliseconds(350);

        var fadeOut = new DoubleAnimation(1, 0, new Duration(fadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var tcs = new TaskCompletionSource<bool>();
        fadeOut.Completed += (_, _) => tcs.TrySetResult(true);

        Dispatcher.Invoke(() => BeginAnimation(OpacityProperty, fadeOut));
        await tcs.Task;

        Dispatcher.Invoke(Close);
    }
}
