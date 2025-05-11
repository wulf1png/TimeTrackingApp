using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace TimeTrackingApp
{
    public partial class FillMonthWindow : Window
    {
        public List<SegmentInfo> WeekdaySegments { get; private set; }
        public List<SegmentInfo> WeekendSegments { get; private set; }
        public bool IncludeWeekends { get; private set; }

        private readonly List<string> StatusOptions = new()
        {
            "На работе", "Обед", "IT проблемы", "Тренинг", "Собрание"
        };

        public FillMonthWindow()
        {
            InitializeComponent();
            AnimateOpen();

            this.Opacity = 0;
            this.Loaded += (s, e) =>
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                this.BeginAnimation(Window.OpacityProperty, anim);
            };

            MonthComboBox.SelectedIndex = DateTime.Now.Month - 1;


            // 📅 Заполняем ComboBox годов
            for (int year = DateTime.Now.Year - 2; year <= DateTime.Now.Year + 5; year++)
            {
                YearComboBox.Items.Add(year);
            }
            YearComboBox.SelectedItem = DateTime.Now.Year;

            foreach (var combo in new[] { WeekdayMorningStatus, WeekdayLunchStatus, WeekdayAfternoonStatus,
                                          WeekendMorningStatus, WeekendLunchStatus, WeekendAfternoonStatus })
            {
                combo.ItemsSource = StatusOptions;
                combo.SelectedIndex = 0;
            }

            IncludeWeekendsCheckBox.Checked += (_, __) => WeekendSettingsPanel.Visibility = Visibility.Visible;
            IncludeWeekendsCheckBox.Unchecked += (_, __) => WeekendSettingsPanel.Visibility = Visibility.Collapsed;
        }

        private void AnimateOpen()
        {
            var anim = new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            WindowTranslate.BeginAnimation(TranslateTransform.YProperty, anim);
        }

        public async Task ShakeWindowAsync()
        {
            for (int i = 0; i < 6; i++)
            {
                this.Left += (i % 2 == 0 ? 6 : -6);
                await Task.Delay(40);
            }
            this.Left -= 6;
        }

        private async void CloseWithFade()
        {
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            this.BeginAnimation(Window.OpacityProperty, fadeOut);
            await Task.Delay(300); // подожди, пока анимация закончится
            this.Close();
        }

        public void SetEmployeeName(string name)
        {
            EmployeeNameTextBlock.Text = $"Изменяется график для: {name}";
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WeekdaySegments = new List<SegmentInfo>
                {
                    new(WeekdayMorningStatus.Text, TimeSpan.Parse(WeekdayMorningStart.Text), TimeSpan.Parse(WeekdayMorningEnd.Text)),
                    new(WeekdayLunchStatus.Text, TimeSpan.Parse(WeekdayLunchStart.Text), TimeSpan.Parse(WeekdayLunchEnd.Text)),
                    new(WeekdayAfternoonStatus.Text, TimeSpan.Parse(WeekdayAfternoonStart.Text), TimeSpan.Parse(WeekdayAfternoonEnd.Text)),
                };

                WeekendSegments = new List<SegmentInfo>
                {
                    new(WeekendMorningStatus.Text, TimeSpan.Parse(WeekendMorningStart.Text), TimeSpan.Parse(WeekendMorningEnd.Text)),
                    new(WeekendLunchStatus.Text, TimeSpan.Parse(WeekendLunchStart.Text), TimeSpan.Parse(WeekendLunchEnd.Text)),
                    new(WeekendAfternoonStatus.Text, TimeSpan.Parse(WeekendAfternoonStart.Text), TimeSpan.Parse(WeekendAfternoonEnd.Text)),
                };

                IncludeWeekends = IncludeWeekendsCheckBox.IsChecked == true;

                DialogResult = true;
                Close();
            }
            catch
            {
                await ShakeWindowAsync();
                MessageBox.Show("Ошибка в формате времени! (например, 09:00)", "Ошибка");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWithFade();
        }

       

        public class SegmentInfo
        {
            public string Status { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }

            public SegmentInfo(string status, TimeSpan start, TimeSpan end)
            {
                Status = status;
                Start = start;
                End = end;
            }
        }
    }
}