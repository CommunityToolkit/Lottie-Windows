using Microsoft.UI.Composition;

namespace LottieWinRT
{
    public interface IAnimatedVisualSource
    {
        IAnimatedVisual? TryCreateAnimatedVisual(Compositor compositor, out object? diagnostics);
    }
}
