using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Enigma.Launchpad.Desktop.Views.Dialogs;

/// <summary>
/// The body of the dialog asking which arguments to run a program with.
/// </summary>
public partial class ArgumentsDialogView : UserControl
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public ArgumentsDialogView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Ready to type over the defaults, or to add to them. Posted, because the dialog is still opening when its
        // content is attached and would take the focus back.
        Dispatcher.UIThread.Post(() =>
        {
            ArgumentsBox.Focus();
            ArgumentsBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
