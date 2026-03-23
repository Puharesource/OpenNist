namespace OpenNist.Viewer.Maui;

using System.Diagnostics.CodeAnalysis;
using Foundation;
using ObjCRuntime;
using UIKit;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Objective-C exported selectors must remain instance members.")]
[Register("AppDelegate")]
internal class AppDelegate : MauiUIApplicationDelegate
{
    private static readonly UIKeyCommand[] NavigationKeyCommands =
    {
        CreateCommand("o", UIKeyModifierFlags.Command, "openDocument:", "Open…"),
        CreateCommand("1", UIKeyModifierFlags.Command, "saveAsLowRateWsq:", "Save As WSQ 0.75"),
        CreateCommand("2", UIKeyModifierFlags.Command, "saveAsHighRateWsq:", "Save As WSQ 2.25"),
        CreateCommand("p", UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, "exportAsPng:", "Export PNG"),
        CreateCommand("j", UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, "exportAsJpeg:", "Export JPEG"),
        CreateCommand("t", UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, "exportAsTiff:", "Export TIFF"),
        CreateCommand("b", UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, "exportAsBmp:", "Export BMP"),
        CreateNavigationCommand(UIKeyCommand.LeftArrow, "navigateToPreviousImage:", "Previous Image"),
        CreateNavigationCommand(UIKeyCommand.RightArrow, "navigateToNextImage:", "Next Image"),
    };

    internal static event EventHandler? OpenRequested;
    internal static event EventHandler? SaveAsLowRateWsqRequested;
    internal static event EventHandler? SaveAsHighRateWsqRequested;
    internal static event EventHandler? ExportAsPngRequested;
    internal static event EventHandler? ExportAsJpegRequested;
    internal static event EventHandler? ExportAsTiffRequested;
    internal static event EventHandler? ExportAsBmpRequested;
    internal static event EventHandler? PreviousImageRequested;
    internal static event EventHandler? NextImageRequested;

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override UIKeyCommand[] KeyCommands => NavigationKeyCommands;

    [Export("openDocument:")]
    private void OpenDocument(UIKeyCommand command)
    {
        OpenRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("saveAsLowRateWsq:")]
    private void SaveAsLowRateWsq(UIKeyCommand command)
    {
        SaveAsLowRateWsqRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("saveAsHighRateWsq:")]
    private void SaveAsHighRateWsq(UIKeyCommand command)
    {
        SaveAsHighRateWsqRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("exportAsPng:")]
    private void ExportAsPng(UIKeyCommand command)
    {
        ExportAsPngRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("exportAsJpeg:")]
    private void ExportAsJpeg(UIKeyCommand command)
    {
        ExportAsJpegRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("exportAsTiff:")]
    private void ExportAsTiff(UIKeyCommand command)
    {
        ExportAsTiffRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("exportAsBmp:")]
    private void ExportAsBmp(UIKeyCommand command)
    {
        ExportAsBmpRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("navigateToPreviousImage:")]
    private void NavigateToPreviousImage(UIKeyCommand command)
    {
        PreviousImageRequested?.Invoke(null, EventArgs.Empty);
    }

    [Export("navigateToNextImage:")]
    private void NavigateToNextImage(UIKeyCommand command)
    {
        NextImageRequested?.Invoke(null, EventArgs.Empty);
    }

    private static UIKeyCommand CreateNavigationCommand(string input, string selectorName, string title)
    {
        return CreateCommand(input, 0, selectorName, title);
    }

    private static UIKeyCommand CreateCommand(string input, UIKeyModifierFlags modifierFlags, string selectorName, string title)
    {
        using var inputString = new NSString(input);
        using var titleString = new NSString(title);
        var command = UIKeyCommand.Create(inputString, modifierFlags, new Selector(selectorName));
        command.DiscoverabilityTitle = titleString;
        command.WantsPriorityOverSystemBehavior = true;
        return command;
    }
}
