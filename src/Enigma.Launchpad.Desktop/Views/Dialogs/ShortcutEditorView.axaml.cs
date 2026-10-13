using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Enigma.Launchpad.Desktop.Views.Dialogs;

/// <summary>
/// The body of the dialog adding or editing a shortcut.
/// </summary>
public partial class ShortcutEditorView : UserControl
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public ShortcutEditorView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Posted, because the dialog is still opening when its content is attached and would take the focus back.
        Dispatcher.UIThread.Post(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
