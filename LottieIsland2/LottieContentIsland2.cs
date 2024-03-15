using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Composition;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace LottieIsland2
{
    public sealed class LottieContentIsland2
    {
        public int MyProperty { get; set; }
        public ContentIsland Island { get => m_island; }
        public IAnimatedVisualSource? AnimatedVisualSource
        {
            get => m_animatedVisualSource;
            set
            {
                m_animatedVisualSource = value;
                object diagnostics;
                if (m_animatedVisualSource != null)
                {
                    IAnimatedVisual animatedVisual = m_animatedVisualSource.TryCreateAnimatedVisual(m_compositor, out diagnostics);

                    // Set up lottie
                    m_rootVisual.Children.InsertAtTop(animatedVisual.RootVisual);
                    var animation = m_compositor.CreateScalarKeyFrameAnimation();
                    animation.Duration = animatedVisual.Duration;
                    var linearEasing = m_compositor.CreateLinearEasingFunction();
                    animation.InsertKeyFrame(0, 0);
                    animation.InsertKeyFrame(1, 1, linearEasing);
                    animation.IterationBehavior = AnimationIterationBehavior.Forever;
                    animatedVisual.RootVisual.Properties.StartAnimation("Progress", animation);
                }
            }
        }

        public string? Uri
        {
            get => m_uri;
            set
            {
                m_uri = value;
                if (m_uri != null)
                {
                    //// THIS LINE causes the SimpleIslandApp to crash
                    //var lottieVisualSource = LottieVisualSource.CreateFromString(m_uri);
                    //if (lottieVisualSource != null)
                    //{
                    //    lottieVisualSource.AnimatedVisualInvalidated += LottieVisualSource_AnimatedVisualInvalidated;
                    //}
                }
            }
        }

        TimeSpan Duration { get; }

        bool IsAnimationLoaded { get; }

        bool IsPlaying { get; }

        public void Pause() { throw new NotImplementedException(); }

        Windows.Foundation.IAsyncAction PlayAsync(Double fromProgress, Double toProgress, Boolean looped) { throw new NotImplementedException(); }

        void Resume() { throw new NotImplementedException(); }

        void Stop() { throw new NotImplementedException(); }

        private Compositor m_compositor;
        private ContainerVisual m_rootVisual;
        private ContentIsland m_island;
        private IAnimatedVisualSource? m_animatedVisualSource;
        private string? m_uri;

        public LottieContentIsland2(Compositor compositor)
        {
            m_compositor = compositor;
            m_rootVisual = m_compositor.CreateContainerVisual();
            m_island = ContentIsland.Create(m_rootVisual);
        }

        private void LottieVisualSource_AnimatedVisualInvalidated(IDynamicAnimatedVisualSource? sender, object? args)
        {
            object? diagnostics = null;
            IAnimatedVisual? animatedVisual = sender?.TryCreateAnimatedVisual(m_compositor, out diagnostics);

            if (m_rootVisual != null)
            {
                m_rootVisual.Children.InsertAtTop(animatedVisual?.RootVisual);
                if (m_compositor != null)
                {
                    var animation = m_compositor.CreateScalarKeyFrameAnimation();
                    if (animatedVisual != null)
                    {
                        animation.Duration = animatedVisual.Duration;
                        var linearEasing = m_compositor.CreateLinearEasingFunction();

                        // Play from beginning to end.
                        animation.InsertKeyFrame(0, 0);
                        animation.InsertKeyFrame(1, 1, linearEasing);

                        animation.IterationBehavior = AnimationIterationBehavior.Forever;

                        // Start the animation and get the controller.
                        animatedVisual.RootVisual.Properties.StartAnimation("Progress", animation);
                    }
                }
            }
        }
    }
}
