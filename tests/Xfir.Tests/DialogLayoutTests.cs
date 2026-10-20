// SPDX-License-Identifier: EUPL-1.2
using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xfir.App;
using Xunit;

namespace Xfir.Tests;

public class DialogLayoutTests
{
    [Theory]
    [InlineData(1366, 768, 1.0)]
    [InlineData(1366, 768, 1.5)]
    [InlineData(1366, 768, 2.0)]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(1920, 1080, 1.5)]
    [InlineData(1920, 1080, 2.0)]
    public void DialogActionsFitInTheAvailableLogicalArea(int pixelsWide, int pixelsHigh, double scale) => Sta(() =>
    {
        var area = new Rect(0, 0, pixelsWide / scale, (pixelsHigh - 40) / scale);
        foreach (var window in new Window[] { new DisclaimerWindow(), new AboutWindow() })
        {
            try
            {
                AddApplicationStyles(window);
                WindowPlacement.FitDialog(window, area);
                Assert.True(window.Width <= area.Width && window.Height <= area.Height);
                var content = (FrameworkElement)window.Content;
                var size = new Size(window.Width - 16, window.Height - 40);
                content.Measure(size);
                content.Arrange(new Rect(size));
                content.UpdateLayout();
                foreach (var button in Descendants(content).OfType<Button>())
                {
                    var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                    Assert.True(bounds.Top >= 0 && bounds.Bottom <= size.Height, $"{window.GetType().Name}: {button.Name} {bounds} / {size}");
                    Assert.True(bounds.Left >= 0 && bounds.Right <= size.Width);
                    Assert.True(button.ActualHeight > 0);
                }
                var scroll = Assert.IsType<ScrollViewer>(window.FindName("ContentScroll"));
                Assert.True(scroll.ActualHeight > 0);
                var qaDirectory = Environment.GetEnvironmentVariable("XFIR_TEST_UI_OUTPUT");
                if (!string.IsNullOrEmpty(qaDirectory) && pixelsWide == 1366 && scale == 2.0)
                {
                    Directory.CreateDirectory(qaDirectory);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale),
                        (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    // The window itself is not shown: render its background behind the client area.
                    var background = new DrawingVisual();
                    using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(size));
                    bitmap.Render(background);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(qaDirectory, window.GetType().Name + ".png"));
                    encoder.Save(output);
                }
            }
            finally { window.Close(); }
        }
    });

    [Fact]
    public void LongNoticeScrollsWithoutPushingTheActionsOutsideTheClientArea() => Sta(() =>
    {
        var window = new DisclaimerWindow();
        try
        {
            AddApplicationStyles(window);
            WindowPlacement.FitDialog(window, new Rect(0, 0, 683, 364));
            window.DisclaimerText.Text = string.Concat(Enumerable.Repeat("Riga di prova per lo scorrimento.\n", 100));
            var content = (FrameworkElement)window.Content;
            var size = new Size(window.Width - 16, window.Height - 40);
            content.Measure(size);
            content.Arrange(new Rect(size));
            content.UpdateLayout();
            Assert.True(window.ContentScroll.ScrollableHeight > 0);
            Assert.True(window.AcceptButton.TransformToAncestor(content).TransformBounds(new Rect(window.AcceptButton.RenderSize)).Bottom <= size.Height);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void RefitsAfterMovingFromANegativeCoordinateMonitorToASmallerArea() => Sta(() =>
    {
        var window = new AboutWindow { Left = -900, Top = 400 };
        try
        {
            WindowPlacement.FitDialog(window, new Rect(-960, 0, 960, 520));
            WindowPlacement.FitDialog(window, new Rect(0, 0, 683, 364));
            Assert.InRange(window.Left, 0, 683 - window.Width);
            Assert.InRange(window.Top, 0, 364 - window.Height);
            Assert.True(window.Width <= 683 && window.Height <= 364);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void AcceptanceCheckboxAndKeyboardDefaultsRemainAvailable() => Sta(() =>
    {
        var window = new DisclaimerWindow();
        try
        {
            Assert.False(window.AcceptButton.IsEnabled);
            window.AcceptCheck.IsChecked = true;
            Assert.True(window.AcceptButton.IsEnabled);
            Assert.True(window.AcceptButton.IsDefault);
            Assert.True(window.DeclineButton.IsCancel);
            Assert.True(window.AcceptCheck.Focusable);
        }
        finally { window.Close(); }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void AddApplicationStyles(Window window)
    {
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Xfir.Tests.AppResources.xaml")!;
        var application = XElement.Load(input);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            application.Element(presentation + "Application.Resources")!.Elements());
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(dictionary.ToString()));
    }

    private static void Sta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = ExceptionDispatchInfo.Capture(e); } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Layout test timed out.");
        error?.Throw();
    }
}
