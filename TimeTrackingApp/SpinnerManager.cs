using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TimeTrackingApp
{
    public class SpinnerManager
    {
        private readonly Control _spinnerControl;
        private readonly Control _statusControl;
        private readonly Control _graphControl;
        private readonly DispatcherTimer _timer;
        private readonly Action _startAnimation;
        private readonly Action _stopAnimation;

        // Конструктор с параметрами
        public SpinnerManager(Control spinnerControl, Control statusControl, Control graphControl, Action startAnimation, Action stopAnimation)
        {
            _spinnerControl = spinnerControl;
            _statusControl = statusControl;
            _graphControl = graphControl;
            _startAnimation = startAnimation;
            _stopAnimation = stopAnimation;
        }

        public void StartSpinnerAnimation()
        {
            _startAnimation.Invoke();
            _spinnerControl.ApplyTemplate();  // Убедитесь, что _spinnerControl это Control, а не просто UIElement
            _statusControl.ApplyTemplate();
            _graphControl.ApplyTemplate();

            var arc = FindArc(_spinnerControl);

            if (arc == null) return;

            var rotate = new RotateTransform(0);
            arc.RenderTransform = rotate;
            arc.RenderTransformOrigin = new Point(0.5, 0.5);

            var spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            spinAnim.SetValue(Timeline.DesiredFrameRateProperty, 60);
            rotate.BeginAnimation(RotateTransform.AngleProperty, spinAnim);

            var fadeAnim = new DoubleAnimation(0.5, 1, TimeSpan.FromSeconds(0.6))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            fadeAnim.SetValue(Timeline.DesiredFrameRateProperty, 60);
            arc.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private Path FindArc(Control spinnerControl)
        {
            return spinnerControl.Template.FindName("Arc", spinnerControl) as Path;
        }
    }
}
