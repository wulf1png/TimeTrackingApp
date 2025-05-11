using LiveChartsCore.Drawing.Segments;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TimeTrackingApp.FirebaseService;
using System.Threading;
using Xceed.Wpf.Toolkit;
using System.Diagnostics;

namespace TimeTrackingApp
{
    public partial class EmployeeView : UserControl, INotifyPropertyChanged
    {
        private readonly string _userId;
        private DateTime _currentMonth;
        public DateTime CurrentMonth
        {
            get => _currentMonth;
            set
            {
                if (_currentMonth != value)
                {
                    _currentMonth = value;
                    OnPropertyChanged(); // уведомляем WPF об изменении
                }
            }
        }
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        private readonly DispatcherTimer _timer;
        private WorkStatusRecord _currentRecord;
        private Rectangle _currentRect;
        private List<ExtendedPlannedSegment> _plannedSegments = new();

        private List<PlannedShift> _monthPlans = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));


        public EmployeeView(string userId)
        {
            InitializeComponent();
            _userId = userId;
            DataContext = this;

            // Начальный месяц и дата
            SelectedDate = DateTime.Today;
            _currentMonth = SelectedDate;
            CurrentMonth = DateTime.Today;

            Loaded += EmployeeView_Loaded;

            HighlightTodayInCalendar();

            // Таймер для «живого» расширения фактического блока и авто-сохранения
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += async (_, __) =>
            {
                if (_currentRecord != null && _currentRect != null)
                {
                    _currentRecord.End = DateTime.Now;
                    UpdateCurrentRect();
                    _currentRect.ToolTip = $"{_currentRecord.Status}: {_currentRecord.Start:HH:mm}–{_currentRecord.End:HH:mm}";

                    // авто-сохранение
                    try
                    {
                        await SaveWorkStatusRecordAsync(_currentRecord);
                    }
                    catch
                    {
                        // игнорируем временные сетевые ошибки
                    }

                    // обновляем нарушения для текущего отрезка
                    DrawViolationsFor(_currentRecord);
                }
            };

            Loaded += async (_, __) =>
            {

                IsLoading = true;
                await Task.Yield();

                var user = await FirebaseService.GetUserByIdAsync(_userId);
                EmployeeNameTextBlock.Text =
                    $"{user.lastName} {user.firstName} {user.patronymic}";
                await BuildCalendarAsync();
                await RedrawAllAsync();

                IsLoading = false;
            };

            Loaded += (_, __) =>
            {
                AppEvents.CalendarNeedsRefresh += async () => await BuildCalendarAsync();
            };

            // 🔥 таймер для линии текущего времени
            var timeLineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timeLineTimer.Tick += UpdateTimeLine;
            timeLineTimer.Start();
        }

        private void EmployeeView_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) Сразу показываем overlay и запускаем спиннер
            IsLoading = true;

            // 2) Запускаем тяжёлую работу в фоне, не блокируя рендер спиннера
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    // пример загрузки профиля
                    var user = await FirebaseService.GetUserByIdAsync(_userId);
                    EmployeeNameTextBlock.Text = $"{user.lastName} {user.firstName} {user.patronymic}";

                    // пример построения календаря и графика
                    await BuildCalendarAsync();
                    await RedrawAllAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
                }
                finally
                {
                    // 3) Скрываем overlay и спиннер
                    IsLoading = false;
                }
            }, DispatcherPriority.Background);

            // 4) Стартуем анимацию спиннера сразу же
            StartSpinnerAnimation();
        }

        private void StartSpinnerAnimation()
        {
            // Применяем шаблон для каждого спиннера
            SpinnerControl.ApplyTemplate();
            SpinnerControl_Status.ApplyTemplate();
            SpinnerControl_Graph.ApplyTemplate();

            // Получаем элемент Path из каждого ContentControl
            var arc1 = SpinnerControl.Template.FindName("Arc1", SpinnerControl) as Path;
            var arc2 = SpinnerControl_Status.Template.FindName("Arc1", SpinnerControl_Status) as Path;
            var arc3 = SpinnerControl_Graph.Template.FindName("Arc1", SpinnerControl_Graph) as Path;

            // Проверяем, что элементы найдены
            if (arc1 == null || arc2 == null || arc3 == null)
            {
                Debug.WriteLine("Не удалось найти один или несколько элементов 'Arc'!");
                return;
            }

            // Создаем новый RotateTransform для каждого Path
            var rotateTransform1 = new RotateTransform();
            var rotateTransform2 = new RotateTransform();
            var rotateTransform3 = new RotateTransform();

            // Устанавливаем новый RotateTransform для каждого элемента
            arc1.RenderTransform = rotateTransform1;
            arc2.RenderTransform = rotateTransform2;
            arc3.RenderTransform = rotateTransform3;

            // Применяем анимацию вращения ко всем спиннерам
            var rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Начинаем анимацию
            rotateTransform1.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            rotateTransform2.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            rotateTransform3.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        }

        // Вспомогательный метод: ищет по дереву визуальных потомков
        private T FindDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name)
                    return fe;
                var result = FindDescendant<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        // ◀ переключение месяца
        private async void PreviousMonth_Click(object sender, RoutedEventArgs e)
        {
            CurrentMonth = CurrentMonth.AddMonths(-1);
            await BuildCalendarAsync();
        }

        // ▶ переключение месяца
        private async void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            CurrentMonth = CurrentMonth.AddMonths(1);
            await BuildCalendarAsync();
        }

        private Dictionary<int, PlannedShift> _planMap;


        // Строим кастомный календарь
        private async Task BuildCalendarAsync()
        {
            //var firstOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            try
            {
                // Показываем overlay с анимацией
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingOverlay_Status.Visibility = Visibility.Visible;
                LoadingOverlay_Graph.Visibility = Visibility.Visible;
                await Dispatcher.Yield(DispatcherPriority.Render);

                var firstOfMonth = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
                int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
                var monthSegments = new Dictionary<int, List<ExtendedPlannedSegment>>();

                // Параллельная загрузка данных
                var tasks = Enumerable.Range(1, daysInMonth).Select(async day =>
                {
                    var date = firstOfMonth.AddDays(day - 1);
                    var segments = await FirebaseService.GetExtendedSegmentsAsync(_userId, date);
                    return (day, segments);
                });

                var results = await Task.WhenAll(tasks);
                foreach (var (day, segments) in results)
                    monthSegments[day] = segments;

                // Подготовка сетки календаря
                CalendarGrid.RowDefinitions.Clear();
                CalendarGrid.Children.Clear();
                for (int r = 0; r < 6; r++)
                    CalendarGrid.RowDefinitions.Add(new RowDefinition());

                int offset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
                int currentDay = 1;

                while (currentDay <= daysInMonth)
                {
                    int idx = offset + (currentDay - 1);
                    int row = idx / 7, col = idx % 7;
                    var date = firstOfMonth.AddDays(currentDay - 1);

                    Brush bg = Brushes.LightGray;
                    if (monthSegments.TryGetValue(currentDay, out var segs) && segs?.Any() == true)
                    {
                        var mainStatus = segs.OrderByDescending(x => (x.End - x.Start).TotalMinutes).First().Status;
                        bg = GetBrushByStatus(mainStatus);
                    }
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                        bg = Brushes.LightGray;

                    var btn = new Button
                    {
                        Content = currentDay.ToString(),
                        Margin = new Thickness(2),
                        Background = bg,
                        Tag = date,
                        BorderBrush = date == SelectedDate ? Brushes.Blue : Brushes.Transparent,
                        BorderThickness = date == SelectedDate ? new Thickness(2) : new Thickness(1)
                    };
                    btn.Click += DayBlock_Click;

                    Grid.SetRow(btn, row);
                    Grid.SetColumn(btn, col);
                    CalendarGrid.Children.Add(btn);

                    currentDay++;
                    await Task.Yield(); // для отрисовки UI между итерациями
                }

                HighlightTodayInCalendar();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при построении календаря: {ex.Message}");
            }
            finally
            {
                // Убираем overlay на UI-потоке
                await Dispatcher.InvokeAsync(() => LoadingOverlay.Visibility = Visibility.Collapsed);
                LoadingOverlay_Status.Visibility = Visibility.Collapsed;
                LoadingOverlay_Graph.Visibility = Visibility.Collapsed;
            }
        }

        private void HighlightTodayInCalendar()
        {
            foreach (var child in CalendarGrid.Children.OfType<Button>())
            {
                if (child.Tag is DateTime date && date.Date == DateTime.Today.Date)
                {
                    child.BorderBrush = Brushes.DeepSkyBlue;
                    child.BorderThickness = new Thickness(3);
                    break;
                }
            }
        }

        private void NormalizePlannedShift(PlannedShift plan, DateTime date)
        {
            plan.Start = plan.Start == default ? date.AddHours(9) : plan.Start;
            plan.End = plan.End == default ? date.AddHours(18) : plan.End;
            plan.LunchStart ??= date.AddHours(13);
            plan.LunchEnd ??= date.AddHours(14);
        }

        // Цвет дня по самой длинной активности
        //private Brush GetDayBrush(DateTime date)
        //{
        //    var recs = GetWorkStatusRecordsAsync(_userId, date).Result;
        //    if (!recs.Any()) return Brushes.LightGray;
        //    var longest = recs.OrderByDescending(r => (r.End - r.Start).TotalHours).First();
        //    return GetBrushByStatus(longest.Status);
        //}

        // Клик по дню
        private async void DayBlock_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            SelectedDate = (DateTime)btn.Tag;

            // Перекрашиваем рамку — без повторной полной перестройки
            HighlightSelectedDay(btn);

            // Обновляем график
            _ = RedrawAllAsync();
        }

        private Button _lastSelectedDayButton;

        private void HighlightSelectedDay(Button btn)
        {
            if (_lastSelectedDayButton != null)
            {
                _lastSelectedDayButton.BorderBrush = Brushes.Transparent;
                _lastSelectedDayButton.BorderThickness = new Thickness(1);
            }
            btn.BorderBrush = Brushes.Blue;
            btn.BorderThickness = new Thickness(2);
            _lastSelectedDayButton = btn;
        }



        // Нажали "Проставить статус"

        private Button _activeStatusButton;

        private async void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = (Button)sender;

            // Нажата та же кнопка — ничего не делаем
            if (_activeStatusButton == clickedButton)
                return;

            var status = clickedButton.Tag as string;
            if (string.IsNullOrEmpty(status)) return;

            var now = DateTime.Now;

            // Завершаем предыдущий статус, если был
            if (_currentRecord != null)
            {
                _timer.Stop();
                _currentRecord.End = now;
                await SaveWorkStatusRecordAsync(_currentRecord);
            }

            // Новый статус
            _currentRecord = new WorkStatusRecord
            {
                UserId = _userId,
                Date = SelectedDate,
                Status = status,
                Start = now,
                End = now
            };
            await SaveWorkStatusRecordAsync(_currentRecord);

            // Визуально — снимаем выделение с предыдущей
            if (_activeStatusButton != null)
            {
                _activeStatusButton.ClearValue(Button.BackgroundProperty);
                _activeStatusButton.ClearValue(Button.ForegroundProperty);
            }

            // Новая активная
            _activeStatusButton = clickedButton;
            _activeStatusButton.Background = Brushes.DarkSlateBlue;
            _activeStatusButton.Foreground = Brushes.Brown;


            // Активируем кнопку завершения смены
            EndShiftButton.IsEnabled = true;

            // Рисуем график
            double width = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = width / 24.0;
            _currentRect = new Rectangle
            {
                Width = 0,
                Height = 20,
                Fill = GetBrushByStatus(status),
                Tag = "fact",
                ToolTip = $"{status}: {now:HH:mm}–{now:HH:mm}"
            };
            Canvas.SetLeft(_currentRect, now.TimeOfDay.TotalHours * step);
            Canvas.SetTop(_currentRect, 70);
            GraphCanvas.Children.Add(_currentRect);

            _timer.Start();

            foreach (var child in StatusButtonsPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.IsEnabled = true;
                    btn.ClearValue(Button.BackgroundProperty);
                    btn.ClearValue(Button.ForegroundProperty);
                }
            }

            // Текущая кнопка — активная, выключаем её
            _activeStatusButton = clickedButton;
            _activeStatusButton.IsEnabled = false; // отключаем hover, press и повторный клик
        }


        // Нажали "Завершить смену"
        private async void EndShiftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRecord == null) return;

            _timer.Stop();
            _currentRecord.End = DateTime.Now;
            await SaveWorkStatusRecordAsync(_currentRecord);
            DrawViolationsFor(_currentRecord);
            _currentRecord = null;
            _currentRect = null;

            // Сбрасываем стиль активной кнопки
            if (_activeStatusButton != null)
            {
                _activeStatusButton.ClearValue(Button.BackgroundProperty);
                _activeStatusButton.ClearValue(Button.ForegroundProperty);
                _activeStatusButton = null;
            }

            // Отключаем кнопку завершения смены
            EndShiftButton.IsEnabled = false;

            // Делаем все статус-кнопки снова кликабельными
            foreach (var child in StatusButtonsPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.IsEnabled = true;
                }
            }
        }

        private void HighlightButton(Button btn)
        {
            foreach (Button child in StatusButtonsPanel.Children.OfType<Button>())
            {
                child.Background = (Brush)FindResource("ButtonDefaultBackground"); // сброс
                child.IsEnabled = true;
            }

            btn.Background = Brushes.LightGreen; // активная
            btn.IsEnabled = false;
            EndShiftButton.IsEnabled = true; // включаем "Завершить смену"
        }

        // Полная перерисовка графика по SelectedDate
        private async Task RedrawAllAsync()
        {
            GraphCanvas.Children.Clear();
            DrawGrid();

            _plannedSegments = await GetExtendedSegmentsAsync(_userId, SelectedDate);

            //if (!_plannedSegments.Any())
            //{
            //    _plannedSegments = new List<ExtendedPlannedSegment>
            //    {
            //        new() { UserId = _userId, Start = SelectedDate.AddHours(9), End = SelectedDate.AddHours(13), Status = "На работе" },
            //        new() { UserId = _userId, Start = SelectedDate.AddHours(13), End = SelectedDate.AddHours(14), Status = "Обед" },
            //        new() { UserId = _userId, Start = SelectedDate.AddHours(14), End = SelectedDate.AddHours(18), Status = "На работе" }
            //    };
            //}

            DrawSchedule(_plannedSegments);

            var recs = await GetWorkStatusRecordsAsync(_userId, SelectedDate);

            foreach (var r in recs)
            {
                DrawFactFor(r);
                DrawViolationsFor(r);
            }

            DrawMissingStatusViolations(recs);

            if (_currentRecord != null && _currentRecord.Date.Date == SelectedDate.Date)
            {
                DrawFactFor(_currentRecord, isCurrent: true);
                DrawViolationsFor(_currentRecord);
                _timer.Start();
            }
        }


        // Обновляем ширину текущего прямоугольника
        private void UpdateCurrentRect()
        {
            double width = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = width / 24.0;
            var w = (_currentRecord.End - _currentRecord.Start).TotalHours * step;
            _currentRect.Width = Math.Max(0, w);
        }

        private Line currentTimeLine;

        private void UpdateTimeLine(object sender, EventArgs e)
        {
            if (currentTimeLine == null) return;

            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = w / 24.0;
            double nowX = DateTime.Now.TimeOfDay.TotalHours * step;

            currentTimeLine.X1 = nowX;
            currentTimeLine.X2 = nowX;
        }


        // Сетка часов 0–24
        private void DrawGrid()
        {
            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double h = GraphCanvas.ActualHeight > 0 ? GraphCanvas.ActualHeight : GraphCanvas.Height;
            double step = w / 24.0;

            // Фоновые полосы
            for (int hour = 0; hour < 24; hour += 2)
            {
                var bg = new Rectangle
                {
                    Width = step * 2,
                    Height = h,
                    Fill = Brushes.LightGray,
                    Opacity = 0.05,  // едва заметно
                };
                Canvas.SetLeft(bg, hour * step);
                Canvas.SetTop(bg, 0);
                GraphCanvas.Children.Add(bg);
            }

            // Линии и подписи
            for (int hour = 0; hour <= 24; hour++)
            {
                double x = hour * step;
                // линия
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = h,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = (hour % 6 == 0 ? 1.5 : 0.5)
                };
                GraphCanvas.Children.Add(line);
                // подпись
                var lbl = new TextBlock
                {
                    Text = $"{hour:00}:00",
                    Foreground = Brushes.Gray,
                    FontSize = 11
                };
                Canvas.SetLeft(lbl, x + 2);
                Canvas.SetTop(lbl, 0);
                GraphCanvas.Children.Add(lbl);
            }
            double nowX = DateTime.Now.TimeOfDay.TotalHours * step;
            // Линия текущего времени
            currentTimeLine = new Line
            {
                X1 = nowX,
                Y1 = 0,
                X2 = nowX,
                Y2 = h,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0)), // полупрозрачный красный
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 0,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    BlurRadius = 3
                }
            };

          

            GraphCanvas.Children.Add(currentTimeLine);
            UpdateTimeLine(null, null);
        }





        //private DispatcherTimer _timer;


        // Расписание: утро, обед, после обеда
        private void DrawSchedule(List<ExtendedPlannedSegment> segments)
        {
            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = w / 24.0;

            foreach (var s in segments)
            {
                var rect = new Rectangle
                {
                    Width = (s.End - s.Start).TotalHours * step,
                    Height = 20,
                    Fill = GetBrushByStatus(s.Status),
                    ToolTip = $"{s.Status}: {s.Start:HH:mm}–{s.End:HH:mm} ({(s.End - s.Start).TotalMinutes} мин.)"
                };
                Canvas.SetLeft(rect, s.Start.TimeOfDay.TotalHours * step);
                Canvas.SetTop(rect, 30);
                GraphCanvas.Children.Add(rect);
            }
        }

        // Фактические блоки статусов
        private void DrawFactFor(WorkStatusRecord r, bool isCurrent = false)
        {
            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = w / 24.0;
            var rect = new Rectangle
            {
                Width = (r.End - r.Start).TotalHours * step,
                Height = 20,
                Fill = GetBrushByStatus(r.Status),
                Tag = "fact",
                ToolTip = $"{r.Status}: {r.Start:HH:mm}–{r.End:HH:mm}"
            };
            Canvas.SetLeft(rect, r.Start.TimeOfDay.TotalHours * step);
            Canvas.SetTop(rect, 70);
            GraphCanvas.Children.Add(rect);

            if (isCurrent) _currentRect = rect;
        }

        // Нарушения по графику
        private void DrawViolationsFor(WorkStatusRecord r)
        {
            if (!_plannedSegments.Any()) return;

            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = w / 24.0;

            bool overlaps = false;

            foreach (var seg in _plannedSegments)
            {
                var overlapStart = Max(r.Start, seg.Start);
                var overlapEnd = Min(r.End, seg.End);

                if (overlapStart < overlapEnd)
                {
                    overlaps = true;

                    // если статус не совпадает
                    if (r.Status != seg.Status)
                    {
                        AddViolationRect(
                            overlapStart.TimeOfDay.TotalHours,
                            overlapEnd.TimeOfDay.TotalHours,
                            step,
                            $"Ожидался '{seg.Status}', а был '{r.Status}'"
                        );
                    }
                }
            }

            // если не было пересечений вообще — значит статус был полностью вне графика
            if (!overlaps)
            {
                AddViolationRect(
                    r.Start.TimeOfDay.TotalHours,
                    r.End.TimeOfDay.TotalHours,
                    step,
                    $"Вне графика: {r.Status}"
                );
            }
        }

        private void AddViolationRect(double startHour, double endHour, double step, string tooltip)
        {
            var viol = new Rectangle
            {
                Width = (endHour - startHour) * step,
                Height = 20,
                Fill = Brushes.IndianRed,
                Opacity = 0.85,
                ToolTip = tooltip
            };
            Canvas.SetLeft(viol, startHour * step);
            Canvas.SetTop(viol, 110);
            GraphCanvas.Children.Add(viol);
        }

        private static DateTime Max(DateTime a, DateTime b) => (a > b) ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => (a < b) ? a : b;

        private void DrawMissingStatusViolations(List<WorkStatusRecord> records)
        {
            if (!_plannedSegments.Any()) return;

            double w = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = w / 24.0;

            var now = DateTime.Now;

            foreach (var seg in _plannedSegments)
            {
                var pointer = seg.Start;
                var end = seg.End > now ? now : seg.End;

                var ordered = records
                    .Where(r => r.End > seg.Start && r.Start < seg.End)
                    .OrderBy(r => r.Start)
                    .ToList();

                foreach (var rec in ordered)
                {
                    if (rec.Start > pointer)
                    {
                        var gapStart = pointer;
                        var gapEnd = rec.Start < end ? rec.Start : end;
                        if (gapEnd > gapStart)
                            AddViolationRect(gapStart.TimeOfDay.TotalHours, gapEnd.TimeOfDay.TotalHours, step, "Нет статуса");
                    }
                    pointer = rec.End > pointer ? rec.End : pointer;
                }

                if (pointer < end)
                    AddViolationRect(pointer.TimeOfDay.TotalHours, end.TimeOfDay.TotalHours, step, "Нет статуса");
            }
        }



        /// <summary>
        /// Обработчик ползунка масштабирования.
        /// </summary>
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GraphCanvas == null) return;

            double scale = e.NewValue;
            GraphCanvas.LayoutTransform = new ScaleTransform(scale, 1);
            //RenderOptions.SetEdgeMode(GraphCanvas, EdgeMode.Aliased); // для чёткости
            //RenderOptions.SetBitmapScalingMode(GraphCanvas, BitmapScalingMode.HighQuality);
        }

        private void ZoomSlider_Loaded(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;

            // Получаем части шаблона
            var thumb = (Thumb)slider.Template.FindName("PART_Thumb", slider);
            var fillTrack = (Rectangle)slider.Template.FindName("PART_FillTrack", slider);
            var backgroundTrack = (Rectangle)slider.Template.FindName("PART_BackgroundTrack", slider);

            if (thumb == null || fillTrack == null || backgroundTrack == null) return;

            void UpdateVisual()
            {
                double percent = (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum);
                double totalWidth = backgroundTrack.ActualWidth;

                fillTrack.Width = totalWidth * percent;

                Canvas.SetLeft(thumb, totalWidth * percent + 10 - thumb.Width / 2); // +10 — это отступ из XAML
            }

            // При изменении значения вручную или программно
            slider.ValueChanged += (_, __) => UpdateVisual();

            // Обновление после первой отрисовки
            backgroundTrack.SizeChanged += (_, __) => UpdateVisual();

            UpdateVisual();
        }


        // Цвет по статусу
        private Brush GetBrushByStatus(string status) => status switch
        {
            "На работе" => Brushes.LimeGreen,
            "Обед" => Brushes.Yellow,
            "IT проблемы" => Brushes.MediumPurple,
            "Тренинг" => Brushes.Blue,
            "Собрание" => Brushes.LightSkyBlue,
            "Отпуск (опл.)" => Brushes.OrangeRed,
            "Отпуск (неопл.)" => Brushes.Gray,
            "Больничный" => Brushes.Red,
            _ => Brushes.LightGray
        };
    }
}