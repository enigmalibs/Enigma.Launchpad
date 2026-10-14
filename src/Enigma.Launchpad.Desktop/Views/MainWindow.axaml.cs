using Avalonia.Controls;

namespace Enigma.Launchpad.Desktop.Views;

/// <summary>
/// The main window: the navigation rail, the page area and the three overlay hosts.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public MainWindow()
    {
        // The generated InitializeComponent is used deliberately: it is what assigns the x:Name'd
        // ContentDialog, Overlay and InfoBar host fields App hands to the Enigma services.
        InitializeComponent();
    }
}
