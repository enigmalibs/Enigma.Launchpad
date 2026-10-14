using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Enigma.Launchpad.Desktop.Views.Dialogs;

/// <summary>
/// The body of the dialog creating or editing a workspace.
/// </summary>
public partial class WorkspaceEditorView : UserControl
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public WorkspaceEditorView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Ready to type: posted, because the dialog is still opening when its content is attached and would take the
        // focus back.
        Dispatcher.UIThread.Post(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
