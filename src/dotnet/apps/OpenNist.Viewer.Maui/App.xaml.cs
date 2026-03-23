namespace OpenNist.Viewer.Maui;

using System.Diagnostics.CodeAnalysis;
using OpenNist.Viewer.Maui.Resources.Styles;

[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "MAUI XAML-generated partial types require public accessibility.")]
public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        Resources.MergedDictionaries.Add(new OpenNistDesignResources());
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage)
        {
            Title = "OpenNist Viewer",
        };
    }
}
