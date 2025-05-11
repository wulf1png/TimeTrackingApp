using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TimeTrackingApp
{
    /// <summary>
    /// Логика взаимодействия для EditStatusWindow.xaml
    /// </summary>
    public partial class EditStatusWindow : Window
    {
        public string SelectedStatus { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public EditStatusWindow()
        {
            InitializeComponent();
            AnimateOpen();
            this.Opacity = 0;
            this.Loaded += (s, e) =>
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                this.BeginAnimation(Window.OpacityProperty, anim);
            };

            StatusTypeCombo.ItemsSource = new[]
            {
            "Отпуск (опл.)", "Отпуск (неопл.)", "Больничный"
        };
            StatusTypeCombo.SelectedIndex = 0;
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;
        }

        private void AnimateOpen()
        {
            var anim = new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            WindowTranslate.BeginAnimation(TranslateTransform.YProperty, anim);
        }

        private async void CloseWithFade()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            this.BeginAnimation(OpacityProperty, fade);
            await Task.Delay(300);
            this.Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (StatusTypeCombo.SelectedItem == null ||
                !StartDatePicker.SelectedDate.HasValue ||
                !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Заполните все поля!");
                return;
            }
            SelectedStatus = StatusTypeCombo.SelectedItem.ToString();
            StartDate = StartDatePicker.SelectedDate.Value;
            EndDate = EndDatePicker.SelectedDate.Value;
            if (EndDate < StartDate)
            {
                MessageBox.Show("Дата окончания раньше начала!");
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWithFade();
        }
    }
}