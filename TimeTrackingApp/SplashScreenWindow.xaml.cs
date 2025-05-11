using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace TimeTrackingApp
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Запускаем fade-in
            var fadeIn = (Storyboard)this.Resources["FadeInStoryboard"];
            fadeIn.Begin(this);  // Указываем окно, к которому применяется анимация

            // Задержка splash экрана 2 секунды
            await Task.Delay(2000);

            // Создаем fade-out анимацию
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(1),
                AutoReverse = false
            };

            // Применяем fade-out анимацию к свойству Opacity окна
            fadeOut.Completed += (s, e) =>
            {
                var login = new LoginWindow();
                login.Show();
                this.Close();
            };

            // Анимируем исчезновение окна
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}