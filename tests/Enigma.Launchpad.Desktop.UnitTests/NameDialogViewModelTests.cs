using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class NameDialogViewModelTests
{
    private static string? RequireName(string? name) => string.IsNullOrWhiteSpace(name) ? "Enter a name." : null;

    [Fact]
    public void ANewEmptyField_IsInvalid_ButShowsNoErrorYet()
    {
        NameDialogViewModel model = new("Group name", null, RequireName);

        Assert.False(model.IsValid);
        Assert.Equal("Enter a name.", model.Error);
        Assert.Null(model.VisibleError);
    }

    [Fact]
    public void ARename_StartsValid_WithTheOldName()
    {
        NameDialogViewModel model = new("Group name", "Dev tools", RequireName);

        Assert.True(model.IsValid);
        Assert.Equal("Dev tools", model.Name);
        Assert.Null(model.VisibleError);
    }

    [Fact]
    public void Typing_RevalidatesAndShowsTheError()
    {
        NameDialogViewModel model = new("Group name", "Dev tools", RequireName);

        model.Name = "   ";

        Assert.False(model.IsValid);
        Assert.Equal("Enter a name.", model.VisibleError);

        model.Name = "  Games ";

        Assert.True(model.IsValid);
        Assert.Null(model.VisibleError);
        Assert.Equal("Games", model.TrimmedName);
    }
}
