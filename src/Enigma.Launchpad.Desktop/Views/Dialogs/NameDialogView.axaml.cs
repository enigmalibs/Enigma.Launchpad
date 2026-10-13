using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Enigma.Launchpad.Desktop.Views.Dialogs;

/// <summary>
/// The body of a dialog asking for one name.
/// </summary>
public partial class NameDialogView : UserControl
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public NameDialogView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Ready to type over: a rename starts with the whole old name selected. Posted, because the dialog
        // is still opening when its content is attached and would take the focus back.
        Dispatcher.UIThread.Post(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
