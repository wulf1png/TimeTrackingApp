using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Firebase.Auth;

namespace TimeTrackingApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            // Сброс
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            LoginBox.Tag = "Normal";
            PasswordBox.Tag = "Normal";

            var login = LoginBox.Text.Trim();
            var password = PasswordBox.Password;

            // Локальная валидация
            if (string.IsNullOrEmpty(login))
            {
                ErrorTextBlock.Text = "Введите логин";
                ErrorTextBlock.Visibility = Visibility.Visible;
                LoginBox.Tag = "Error";
                LoginBox.Focus();
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                ErrorTextBlock.Text = "Введите пароль";
                ErrorTextBlock.Visibility = Visibility.Visible;
                PasswordBox.Tag = "Error";
                PasswordBox.Focus();
                return;
            }

            // Заблокировать кнопку, чтобы не нажимали несколько раз
            ((FrameworkElement)sender).IsEnabled = false;

            try
            {
                var (userId, role) = await FirebaseService.AuthenticateUser(login, password);

                if (userId == null)
                {
                    ShowError("Неверный логин или пароль");
                    LoginBox.Tag = "Error";
                    PasswordBox.Tag = "Error";
                    return;
                }

                // Успешно
                var mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.ShowRoleInterface(role, userId);
                Close();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при входе — попробуйте снова");
                Debug.WriteLine(ex);
            }
            finally
            {
                ((FrameworkElement)sender).IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}