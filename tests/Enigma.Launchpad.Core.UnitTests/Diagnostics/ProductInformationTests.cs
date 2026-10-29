using System;
using Enigma.Launchpad.Core.Diagnostics;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Diagnostics;

public sealed class ProductInformationTests
{
    [Fact]
    public void Version_IsTheRunningVersion_WithoutTheRevision()
    {
        Assert.False(string.IsNullOrWhiteSpace(ProductInformation.Version));
        Assert.DoesNotContain("+", ProductInformation.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_IsTheSolutionsVersion()
        => Assert.StartsWith(
            typeof(ProductInformation).Assembly.GetName().Version!.ToString(3),
            ProductInformation.Version,
            StringComparison.Ordinal);

    [Fact]
    public void DisplayName_IsTheProductInWords()
        => Assert.Equal("Enigma Launchpad", ProductInformation.DisplayName);

    [Fact]
    public void Copyright_IsTheOneTheBuildStamps()
        => Assert.Contains("Josué Clément", ProductInformation.Copyright, StringComparison.Ordinal);
}
