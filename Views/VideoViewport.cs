using System;
using System.Windows;
using System.Windows.Controls;

namespace uYouWin.Views
{
    /// <summary>
    /// Gives the native video host finite layout bounds without allowing
    /// its natural media size to determine the surrounding page layout.
    /// </summary>
    public sealed class VideoViewport : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = new Size(
                double.IsInfinity(availableSize.Width) ? 640 : Math.Max(0, availableSize.Width),
                double.IsInfinity(availableSize.Height) ? 360 : Math.Max(0, availableSize.Height));
            foreach (UIElement child in InternalChildren)
            {
                if (child is FrameworkElement element)
                {
                    element.Width = size.Width;
                    element.Height = size.Height;
                }
                ConstrainNativeVideo(child, size);
                child.Measure(size);
            }
            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                ConstrainNativeVideo(child, finalSize);
                if (child is FrameworkElement element &&
                    (element.Width != finalSize.Width || element.Height != finalSize.Height))
                {
                    element.Width = finalSize.Width;
                    element.Height = finalSize.Height;
                    child.Measure(finalSize);
                }
                child.Arrange(new Rect(new Point(), finalSize));
            }
            return finalSize;
        }

        private static void ConstrainNativeVideo(UIElement child, Size size)
        {
            var host = child as Microsoft.Toolkit.Wpf.UI.Controls.MediaPlayerElement;
            var native = host?.GetUwpInternalObject() as Windows.UI.Xaml.Controls.MediaPlayerElement;
            if (native == null || size.Width <= 0 || size.Height <= 0)
                return;

            // Toolkit 6.1.2 passes these same logical units to UWP Measure/Arrange.
            // Constrain the renderer as well as the HWND so natural media size cannot overflow it.
            native.IsFullWindow = false;
            native.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
            SetNativeBounds(native, size);
            native.ApplyTemplate();
            ConstrainPresenters(native, size);
        }

        private static void SetNativeBounds(Windows.UI.Xaml.FrameworkElement element, Size size)
        {
            element.MinWidth = 0;
            element.MinHeight = 0;
            element.MaxWidth = size.Width;
            element.MaxHeight = size.Height;
            element.Width = size.Width;
            element.Height = size.Height;
            element.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch;
            element.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Stretch;
        }

        private static void ConstrainPresenters(Windows.UI.Xaml.DependencyObject parent, Size size)
        {
            int count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is Windows.UI.Xaml.Controls.MediaPlayerPresenter presenter)
                {
                    presenter.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
                    SetNativeBounds(presenter, size);
                }
                else
                {
                    ConstrainPresenters(child, size);
                }
            }
        }
    }
}
