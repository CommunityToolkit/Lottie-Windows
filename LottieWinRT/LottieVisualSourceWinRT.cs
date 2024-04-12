using System.Diagnostics;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;

namespace LottieWinRT
{
    public sealed class LottieVisualSourceWinRT
    {
        public event EventHandler<object?>? AnimatedVisualInvalidated;

        private LottieVisualSourceFrameworkless _lottieVisualSource;

        public LottieVisualSourceWinRT()
        {
            _lottieVisualSource = new LottieVisualSourceFrameworkless();
        }

        private LottieVisualSourceWinRT(LottieVisualSourceFrameworkless lottieVisualSource)
        {
            _lottieVisualSource = lottieVisualSource;
            _lottieVisualSource.AnimatedVisualInvalidated += (IAnimatedVisualSource? sender, object? o) =>
            {
                AnimatedVisualInvalidated?.Invoke(this, o);
            };
        }

        public static LottieVisualSourceWinRT? CreateFromString(string uri)
        {
            LottieVisualSourceFrameworkless? lottieSource = LottieVisualSourceFrameworkless.CreateFromString(uri);
            if (lottieSource == null)
            {
                return null;
            }

            LottieVisualSourceWinRT winrtSource = new LottieVisualSourceWinRT(lottieSource);

            return winrtSource;
        }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSource"/>.
        /// </summary>
        public Uri? UriSource
        {
            get => _lottieVisualSource?.UriSource;
            set
            {
                if (_lottieVisualSource != null)
                {
                    _lottieVisualSource.UriSource = value;
                }
            }
        }

        /// <summary>
        /// Implements <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <param name="compositor">The <see cref="Compositor"/> that can be used as a factory for the resulting <see cref="IAnimatedVisual"/>.</param>
        /// <param name="diagnostics">An optional object that may provide extra information about the result.</param>
        /// <returns>An <see cref="IAnimatedVisual"/>.</returns>
        public AnimatedVisualWinRT? TryCreateAnimatedVisual(
            Compositor compositor,
            out object? diagnostics)
        {
            diagnostics = null;
            IAnimatedVisual? visual = _lottieVisualSource?.TryCreateAnimatedVisual(compositor, out diagnostics);
            if (visual == null)
            {
                return null;
            }

            return new AnimatedVisualWinRT(visual);
        }
    }
}

//namespace LottieWinRT
//{
//    public sealed class LottieVisualSourceWinRT
//    {
//        public event EventHandler<object>? AnimatedVisualInvalidated;

//        public void LoadLottie(string uri)
//        {
//            LottieVisualSourceFrameworkless? source = LottieVisualSourceFrameworkless.CreateFromString(uri);
//            if (source != null)
//            {
//                source.AnimatedVisualInvalidated += Source_AnimatedVisualInvalidated;
//                LottieVisualSourceFrameworkless.CreateFromString("meep");
//            }

//            _lottieVisualSource = source;
//        }

//        private void Source_AnimatedVisualInvalidated(IAnimatedVisualSource? sender, object? args)
//        {
//            this.AnimatedVisualInvalidated?.Invoke(this, EventArgs.Empty);
//        }

//        public CommunityToolkit.WinAppSDK.Frameworkless.Lottie.IAnimatedVisualSource? AnimatedVisual { get => _lottieVisualSource; }

//        Compositor? _compositor;
//        ContainerVisual? _rootVisual;
//        LottieVisualSourceFrameworkless? _lottieVisualSource;

//        public void SetUpLottie(Compositor compositor, ContainerVisual parent, string uri)
//        {
//            _compositor = compositor;
//            _rootVisual = parent;

//            _lottieVisualSource = LottieVisualSourceFrameworkless.CreateFromString(uri);
//            if (_lottieVisualSource != null)
//            {
//                _lottieVisualSource.AnimatedVisualInvalidated += LottieVisualSource_AnimatedVisualInvalidated;
//                object? diagnostics = null;
//                if (_lottieVisualSource.TryCreateAnimatedVisual(_compositor, out diagnostics) != null)
//                {
//                    LottieVisualSource_AnimatedVisualInvalidated(_lottieVisualSource, null);
//                }
//            }
//        }

//        private void LottieVisualSource_AnimatedVisualInvalidated(IAnimatedVisualSource? sender, object? args)
//        {
//            if (_compositor != null)
//            {
//                object? diagnostics = null;
//                IAnimatedVisual? animatedVisual = sender?.TryCreateFrameworklessAnimatedVisual(_compositor, out diagnostics);

//                if (_rootVisual != null)
//                {
//                    _rootVisual.Children.RemoveAll();
//                    _rootVisual.Children.InsertAtTop(animatedVisual?.RootVisual);
//                    Debug.WriteLine("Added Lottie visual to root. beep boop");

//                    if (_compositor != null)
//                    {
//                        var animation = _compositor.CreateScalarKeyFrameAnimation();
//                        if (animatedVisual != null)
//                        {
//                            animation.Duration = animatedVisual.Duration;
//                            var linearEasing = _compositor.CreateLinearEasingFunction();

//                            // Play from beginning to end.
//                            animation.InsertKeyFrame(0, 0);
//                            animation.InsertKeyFrame(1, 1, linearEasing);

//                            animation.IterationBehavior = AnimationIterationBehavior.Forever;

//                            // Start the animation and get the controller.
//                            animatedVisual.RootVisual?.Properties.StartAnimation("Progress", animation);
//                        }
//                    }
//                }
//            }
//        }
//    }
//}
