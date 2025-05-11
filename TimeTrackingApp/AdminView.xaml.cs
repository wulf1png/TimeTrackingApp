using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Firebase.Database;
using static TimeTrackingApp.FirebaseService;

namespace TimeTrackingApp
{
    public partial class AdminView : UserControl
    {
        public AdminView()
        {
            InitializeComponent();
            _ = LoadLogsAsync();
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            // Скрыть старую ошибку
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            InfoTextBlock.Visibility = Visibility.Collapsed;

            // 1) Проверка пустых полей
            if (string.IsNullOrWhiteSpace(FirstNameBox.Text))
            { ShowError("Введите имя", FirstNameBox); return; }
            if (string.IsNullOrWhiteSpace(LastNameBox.Text))
            { ShowError("Введите фамилию", LastNameBox); return; }
            if (string.IsNullOrWhiteSpace(EmailBox.Text))
            { ShowError("Введите email", EmailBox); return; }
            if (string.IsNullOrWhiteSpace(PhoneBox.Text))
            { ShowError("Введите телефон", PhoneBox); return; }
            if (string.IsNullOrWhiteSpace(LoginBox.Text))
            { ShowError("Введите логин", LoginBox); return; }
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            { ShowError("Введите пароль", PasswordBox); return; }

            // 2) Получаем всех пользователей, словарь или массив
            var allUsers = await FirebaseService.GetAllUsersAsync();

            // 3) Проверяем уникальность логина (без учёта регистра)
            var desired = LoginBox.Text.Trim().ToLowerInvariant();
            if (allUsers.Any(u =>
                !string.IsNullOrWhiteSpace(u.User.login) &&
                u.User.login.Trim().ToLowerInvariant() == desired))
            {
                ShowError("Логин уже занят", LoginBox);
                return;
            }

            // 4) Выделяем новый числовой ID
            var newId = await FirebaseService.AllocateNextUserIdAsync();

            // 5) Формируем объект
            var newUser = new UserData
            {
                firstName = FirstNameBox.Text.Trim(),
                lastName = LastNameBox.Text.Trim(),
                patronymic = PatronymicBox.Text.Trim(),
                email = EmailBox.Text.Trim(),
                phone = PhoneBox.Text.Trim(),
                login = LoginBox.Text.Trim(),
                password = PasswordBox.Password,
                role = ((ComboBoxItem)RoleBox.SelectedItem)?.Content.ToString()
                             ?? "employee"
            };

            // 6) Сохраняем в Firebase
            await FirebaseService.SaveUserAsync(newId.ToString(), newUser);

            // 7) Логируем
            await FirebaseService.PostLogAsync(
                $"[{DateTime.Now:dd.MM HH:mm}] Зарегистрирован '{newUser.login}'");

            InfoTextBlock.Text = $"Пользователь '{newUser.login}' успешно зарегистрирован (Uid={newId}).";
            InfoTextBlock.Visibility = Visibility.Visible;

            // 8) Сброс формы, подгрузка логов
            ClearForm();
            await LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            LogsBox.Items.Clear();
            try
            {
                var raw = await FirebaseService.GetLogsRawAsync();
                foreach (var entry in raw)
                    LogsBox.Items.Add(entry.Object);
            }
            catch
            {
                LogsBox.Items.Add("Ошибка загрузки логов.");
            }
        }

        private void ShowError(string message, FrameworkElement field)
        {
            // Скрываем информационный блок
            InfoTextBlock.Visibility = Visibility.Collapsed;

            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;

            // Сброс всех Tag-меток
            foreach (var tb in new FrameworkElement[]
            {
        FirstNameBox, LastNameBox, PatronymicBox,
        EmailBox, PhoneBox, LoginBox, PasswordBox
            })
            {
                tb.Tag = "Normal";
            }

            field.Tag = "Error";
            field.Focus();

            // Обновить визуализацию рамок сразу
            field.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Render,
                new Action(() => { })
            );
        }

        private void ClearForm()
        {
            FirstNameBox.Clear(); FirstNameBox.Tag = "Normal";
            LastNameBox.Clear(); LastNameBox.Tag = "Normal";
            PatronymicBox.Clear(); PatronymicBox.Tag = "Normal";
            EmailBox.Clear(); EmailBox.Tag = "Normal";
            PhoneBox.Clear(); PhoneBox.Tag = "Normal";
            LoginBox.Clear(); LoginBox.Tag = "Normal";
            PasswordBox.Clear(); PasswordBox.Tag = "Normal";
            RoleBox.SelectedIndex = 0;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }
    }
}