using DocumentFormat.OpenXml.Drawing.Charts;
using System.Windows;
using System.Windows.Media.Animation;

namespace TimeTrackingApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Opacity = 0; // Для fade-in
        }

        double width = 800, height = 600;
        


        public void ShowRoleInterface(string role, string userId)
        {
            switch (role.ToLower())
            {
                case "employee":
                case "сотрудник":
                    RoleContent.Content = new EmployeeView(userId);
                    AnimateWindowAppearance(770, 1060);
                    break;
                case "manager":
                case "руководитель":
                    RoleContent.Content = new ManagerCombinedView(userId);
                    AnimateWindowAppearance(1600, 1100);
                    break;
                case "accountant":
                case "бухгалтер":
                    RoleContent.Content = new AccountantCombinedView(userId);
                    AnimateWindowAppearance(1600, 1100);
                    break;
                case "admin":
                case "админ":
                    RoleContent.Content = new AdminView();
                    AnimateWindowAppearance(620, 750);
                    break;
            }

            // Центрируем окно на экране (если хочешь красиво)
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            //this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2 + SystemParameters.WorkArea.Left;
            //this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2 + SystemParameters.WorkArea.Top;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Можно добавлять логику для других изменений размеров, если нужно
            var newWidth = e.NewSize.Width;
            var newHeight = e.NewSize.Height;

            // Например, можно обновить элементы интерфейса в зависимости от размера окна
        }

        private void AnimateWindowAppearance(double targetWidth, double targetHeight)
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            var targetLeft = (screenWidth - targetWidth) / 2 + SystemParameters.WorkArea.Left;
            var targetTop = (screenHeight - targetHeight) / 2 + SystemParameters.WorkArea.Top;

            var duration = TimeSpan.FromMilliseconds(400);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // Плавная позиция
            var animLeft = new DoubleAnimation(targetLeft, duration) { EasingFunction = easing };
            var animTop = new DoubleAnimation(targetTop, duration) { EasingFunction = easing };

            // Плавный размер
            var animWidth = new DoubleAnimation(targetWidth, duration) { EasingFunction = easing };
            var animHeight = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };

            // Fade-in эффект
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            // Применяем
            this.BeginAnimation(Window.LeftProperty, animLeft);
            this.BeginAnimation(Window.TopProperty, animTop);
            this.BeginAnimation(Window.WidthProperty, animWidth);
            this.BeginAnimation(Window.HeightProperty, animHeight);
            this.BeginAnimation(Window.OpacityProperty, fade);
        }






    }
}