using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TimeTrackingApp.FirebaseService;

namespace TimeTrackingApp
{

    public class EditableSegment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Status { get; set; }
    }

    public partial class ManagerView : UserControl, INotifyPropertyChanged
    {
        private readonly string _userId;
        private readonly DispatcherTimer _timer;
        private string _selectedUserId;
        private DateTime _selectedDate;
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        private DateTime _currentMonth;
        private WorkStatusRecord _currentRecord;
        private Rectangle _currentRect;
        private FirebaseService.PlannedShift _plannedShift;

        public string SelectedEmployeeId { get; set; }
        // Словари для истории «факта» и «нарушений»
        private readonly Dictionary<string, Rectangle> _factRects = new();
        private readonly Dictionary<string, Rectangle> _violRects = new();
        private Line _nowLine;
        private readonly DispatcherTimer _liveTimer;

        public ObservableCollection<EmployeeInfo> Employees { get; } = new();
        public ICollectionView EmployeesView { get; }


        public ObservableCollection<EditableSegment> Segments { get; } = new();
        public List<string> StatusOptions { get; } = new()
{
    "На работе",
    "Обед",
    "IT проблемы",
    "Тренинг",
    "Собрание",
    "Отпуск (опл.)",
    "Отпуск (неопл.)",
    "Больничный"
};

        private bool _isDropDownOpen;
        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set { _isDropDownOpen = value; OnPropertyChanged(); }
        }


        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                EmployeesView.Refresh();
                IsDropDownOpen = !string.IsNullOrWhiteSpace(_searchText);
            }
        }



        private ObservableCollection<ReportItem> _reportItems = new();

       

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));




        public ManagerView(string userId)
        {
            InitializeComponent();
            DataContext = this;
            _userId = userId;
            Loaded += OnLoaded;


            SelectedDate = DateTime.Today;
            _currentMonth = SelectedDate;




            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveTimer.Tick += async (_, __) =>
            {
                await UpdateLiveAsync();
            };
            _liveTimer.Start();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) =>
            {
                if (_currentRecord != null && _currentRect != null)
                {
                    _currentRecord.End = DateTime.Now;
                    UpdateCurrentRect();
                    DrawViolationsFor(_currentRecord);
                }
            };

            // Таймер для линии «сейчас»
            var timeLineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timeLineTimer.Tick += UpdateTimeLine;
            timeLineTimer.Start();

            // (По желанию) таймер для загрузки завершённых записей
            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveTimer.Tick += async (_, __) => await UpdateLiveAsync();
            _liveTimer.Start();

            //Loaded += async (_, __) =>
            //{

            //    var user = await FirebaseService.GetUserByIdAsync(_userId);
            //    await RedrawAllAsync();
            //};



            //// 🔥 таймер для линии текущего времени
            //var timeLineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            //timeLineTimer.Tick += UpdateTimeLine;
            //timeLineTimer.Start();

            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            EmployeesView.Filter = o =>
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return true;
                var emp = (EmployeeInfo)o;
                return emp.FullName
                          .IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            _ = LoadEmployeesAsync();

        }

        private async Task LoadEmployeesAsync()
        {
            var list = await FirebaseService.GetAllEmployeesAsync();
            Employees.Clear();
            foreach (var e in list) Employees.Add(e);

            // Сбросить строку поиска и дропдаун
            SearchText = "";
            IsDropDownOpen = false;
            SelectedEmployeeId = Employees.Any() ? Employees.First().Uid : null;
        }

        public ICommand RemoveSegmentCommand => new RelayCommand(param =>
        {
            if (param is Guid id)
            {
                var seg = Segments.FirstOrDefault(s => s.Id == id);
                if (seg != null) Segments.Remove(seg);
            }
        });


        private List<ExtendedPlannedSegment> _plannedSegments = new();
        private async Task RedrawAllAsync()
        {
            GraphCanvas.Children.Clear();
            DrawGrid();

            _plannedSegments = await FirebaseService.GetExtendedSegmentsAsync(_selectedUserId, _selectedDate);

            // ❌ Убираем автоматическое создание дефолтных сегментов
            // if (!_plannedSegments.Any()) { ... } — УДАЛИТЬ!!!

            Segments.Clear();
            foreach (var seg in _plannedSegments)
            {
                Segments.Add(new EditableSegment
                {
                    Start = seg.Start.TimeOfDay,
                    End = seg.End.TimeOfDay,
                    Status = seg.Status
                });
            }

            // Если сегменты всё-таки есть — рисуем расписание
            if (_plannedSegments.Any())
            {
                DrawSchedule(_plannedSegments);
            }

            // Фактические записи
            var records = await FirebaseService.GetWorkStatusRecordsAsync(_selectedUserId, _selectedDate);
            foreach (var r in records)
            {
                DrawFactFor(r);
                DrawViolationsFor(r);
            }

            DrawMissingStatusViolations(records);

            if (_currentRecord != null && _currentRecord.Date.Date == _selectedDate.Date)
            {
                DrawFactFor(_currentRecord, isCurrent: true);
                DrawViolationsFor(_currentRecord);
                _timer.Start();
            }
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

        private async void SaveSegments_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedUserId) || _selectedDate == default) return;

            // Если сегментов вообще нет — значит день выходной!
            if (!Segments.Any())
            {
                // Просто удаляем все плановые сегменты на этот день
                await FirebaseService.SaveExtendedSegmentsAsync(_selectedUserId, _selectedDate, new List<ExtendedPlannedSegment>());

                MessageBox.Show("День сохранён как выходной.");
            }
            else
            {
                // Иначе сохраняем сегменты как обычно
                var segmentsToSave = Segments
                    .Select(s => new ExtendedPlannedSegment
                    {
                        UserId = _selectedUserId,
                        Start = _selectedDate.Date + s.Start,
                        End = _selectedDate.Date + s.End,
                        Status = s.Status
                    })
                    .ToList();

                await FirebaseService.SaveExtendedSegmentsAsync(_selectedUserId, _selectedDate, segmentsToSave);

                MessageBox.Show("Сегменты сохранены.");
            }

            await RedrawAllAsync();
            AppEvents.RaiseCalendarRefresh();
        }

        private void AddSegment_Click(object sender, RoutedEventArgs e)
        {
            var last = Segments.LastOrDefault();
            var from = last != null ? last.End : TimeSpan.FromHours(9); // <--- исправил
            Segments.Add(new EditableSegment
            {
                Start = from,
                End = from.Add(TimeSpan.FromHours(1)), // <--- исправил
                Status = StatusOptions.First()
            });
        }

        // Удалить сегмент по Id из Tag кнопки
        private void RemoveSegment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                var seg = Segments.FirstOrDefault(s => s.Id == id);
                if (seg != null) Segments.Remove(seg);
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

            double width = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = width / 24.0;
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
        }





        //private DispatcherTimer _timer;


        // Расписание: утро, обед, после обеда
        private void DrawSchedule(List<ExtendedPlannedSegment> segments)
        {
            double width = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : GraphCanvas.Width;
            double step = width / 24.0;

            foreach (var seg in segments.OrderBy(s => s.Start))
            {
                var rect = new Rectangle
                {
                    Width = (seg.End - seg.Start).TotalHours * step,
                    Height = 20,
                    Fill = GetBrushByStatus(seg.Status),
                    Tag = "plan",
                    ToolTip = $"{seg.Status}: {seg.Start:HH:mm}–{seg.End:HH:mm} ({(seg.End - seg.Start).TotalMinutes} мин.)"
                };

                Canvas.SetLeft(rect, seg.Start.TimeOfDay.TotalHours * step);
                Canvas.SetTop(rect, 30); // Полоса планов всегда на одной линии
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

        private static DateTime Max(DateTime a, DateTime b) => (a > b) ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => (a < b) ? a : b;


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
            RenderOptions.SetEdgeMode(GraphCanvas, EdgeMode.Aliased); // для чёткости
            RenderOptions.SetBitmapScalingMode(GraphCanvas, BitmapScalingMode.HighQuality);

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


        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Заполняем список сотрудников
            var list = await FirebaseService.GetAllEmployeesAsync();

            Employees.Clear();
            foreach (var emp in list)
                Employees.Add(emp);

            // Сбросим выбор и поиск
            SearchText = "";
            IsDropDownOpen = false;
            SelectedEmployeeId = Employees.Any() ? Employees.First().Uid : null;

            // Остальной ваш код, если нужен…
            _selectedDate = DateTime.Today;
            datePicker.SelectedDate = _selectedDate;
            await RefreshAllAsync();
            _liveTimer.Start();
        }

        private async void EmployeeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => await RefreshAllAsync();

        private async void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
            => await RefreshAllAsync();

        private async Task RefreshAllAsync()
        {
            if (EmployeeComboBox.SelectedValue is string uid && datePicker.SelectedDate.HasValue)
            {
                _selectedUserId = uid;
                _selectedDate = datePicker.SelectedDate.Value;
                await RedrawAllAsync();
            }
        }



        /// <summary>
        /// Живое обновление: передвигаем линию «сейчас» и подгоняем прямоугольники
        /// </summary>
        private async Task UpdateLiveAsync()
        {
            UpdateNowLine();

            var records = await FirebaseService.GetWorkStatusRecordsAsync(_selectedUserId, _selectedDate);
            foreach (var r in records)
            {
                CreateOrUpdateFactRect(r);
                CreateOrUpdateViolRect(r);
                DrawViolationsFor(r);
                
            }
        }

        #region Drawing Helpers



        private void UpdateNowLine()
        {
            if (_nowLine == null) { return; }
            double step = GraphCanvas.Width / 24.0;
            double nowX = DateTime.Now.TimeOfDay.TotalHours * step;
            _nowLine.X1 = _nowLine.X2 = nowX;
        }


        private void CreateOrUpdateFactRect(WorkStatusRecord r)
        {
            var key = r.FirebaseKey ?? $"{r.Start.Ticks}";
            double step = GraphCanvas.Width / 24.0;
            double width = ((r.End == default ? DateTime.Now : r.End) - r.Start).TotalHours * step;
            double left = r.Start.TimeOfDay.TotalHours * step;

            if (_factRects.TryGetValue(key, out var rect))
            {
                rect.Width = width;
                Canvas.SetLeft(rect, left);
                rect.ToolTip = $"{r.Status}: {r.Start:HH:mm}–{r.End:HH:mm}";
                rect.Fill = GetBrushByStatus(r.Status);
            }
            else
            {
                rect = new Rectangle
                {
                    Width = width,
                    Height = 20,
                    Fill = GetBrushByStatus(r.Status),
                    Tag = "fact",
                    ToolTip = $"{r.Status}: {r.Start:HH:mm}–{r.End:HH:mm}"
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, 70);
                GraphCanvas.Children.Add(rect);
                _factRects[key] = rect;
            }
        }

        private void CreateOrUpdateViolRect(WorkStatusRecord r)
        {
            if (_plannedShift == null) return;

            // Фактический промежуток
            double rs = r.Start.TimeOfDay.TotalHours;
            DateTime actualEnd = (r.End > r.Start) ? r.End : DateTime.Now;
            double re = actualEnd.TimeOfDay.TotalHours;

            // Если запись нулевая или отрицательная длина — ничего не рисуем
            if (re <= rs) return;

            // Проверяем, есть ли хоть одна минута несоответствия между статусом и ожиданием по плану
            int totalMinutes = (int)Math.Round((re - rs) * 60);
            bool mismatch = false;
            for (int i = 0; i < totalMinutes; i++)
            {
                var t = r.Start.AddMinutes(i);
                // определяем, какой статус ожидался по плану именно в этот момент
                bool isLunch = _plannedShift.LunchStart.HasValue
                                 && t.TimeOfDay >= _plannedShift.LunchStart.Value.TimeOfDay
                                 && t.TimeOfDay < _plannedShift.LunchEnd.Value.TimeOfDay;
                var expected = isLunch ? "Обед" : "На работе";
                if (r.Status != expected)
                {
                    mismatch = true;
                    break;
                }
            }
            if (!mismatch) return;

            // Ключ для кеширования прямоугольника
            var key = (r.FirebaseKey ?? r.Start.Ticks.ToString()) + "_viol";
            double step = GraphCanvas.Width / 24.0;
            double width = (re - rs) * step;
            double left = rs * step;

            // Обновляем уже существующий или создаём новый
            if (_violRects.TryGetValue(key, out var rect))
            {
                rect.Width = width;
                Canvas.SetLeft(rect, left);
            }
            else
            {
                rect = new Rectangle
                {
                    Width = width,
                    Height = 20,
                    Fill = Brushes.IndianRed,
                    Tag = "viol",
                    ToolTip = $"Нарушение: {r.Start:HH:mm}–{actualEnd:HH:mm}"
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, 110);
                GraphCanvas.Children.Add(rect);
                _violRects[key] = rect;
            }
        }

        private async void FillMonthButton_ClicllMonthButton_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeComboBox.SelectedValue is not string uid) return;

            var selectedEmployee = (EmployeeComboBox.SelectedItem as EmployeeInfo)?.login ?? "Сотрудник";

            var window = new FillMonthWindow
            {
                Owner = Application.Current.MainWindow
            };

            window.SetEmployeeName(selectedEmployee);

            if (window.ShowDialog() == true)
            {
                var loading = new LoadingWindow
                {
                    Owner = Application.Current.MainWindow
                };

                LoadingOverlay.Visibility = Visibility.Visible;
                FillMonthButton.IsEnabled = false;
                SaveSegmentsButton.IsEnabled = false;
                

                try
                {
                    loading.Show();

                    // 🗓 Используем выбранный месяц из FillMonthWindow
                    int selectedMonth = window.MonthComboBox.SelectedIndex + 1;
                    int selectedYear = (int)window.YearComboBox.SelectedItem;

                    var firstDay = new DateTime(selectedYear, selectedMonth, 1);
                    var daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);

                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        var date = firstDay.AddDays(day - 1);
                        bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                        if (isWeekend && !window.IncludeWeekends)
                            continue;

                        var segments = new List<ExtendedPlannedSegment>();
                        var sourceSegments = isWeekend ? window.WeekendSegments : window.WeekdaySegments;

                        foreach (var s in sourceSegments)
                        {
                            segments.Add(new ExtendedPlannedSegment
                            {
                                UserId = uid,
                                Start = date.Date + s.Start,
                                End = date.Date + s.End,
                                Status = s.Status
                            });
                        }

                        await FirebaseService.SaveExtendedSegmentsAsync(uid, date, segments);

                        double progress = (double)day / daysInMonth * 100;
                        loading.SetProgress(progress, $"День {day}/{daysInMonth}");
                    }

                    await RefreshAllAsync();
                    AppEvents.RaiseCalendarRefresh();

                    loading.SetProgress(100, "Готово!");
                    await Task.Delay(500);
                }
                finally
                {
                    loading.Close();
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    FillMonthButton.IsEnabled = true;
                    SaveSegmentsButton.IsEnabled = true;
                }
            }
        }

        private async void AddCustomStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeComboBox.SelectedValue is not string uid) return;

            var dlg = new EditStatusWindow { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true)
                return;

            string status = dlg.SelectedStatus;
            DateTime fromDate = dlg.StartDate.Date;
            DateTime toDate = dlg.EndDate.Date;

            var loading = new LoadingWindow { Owner = Application.Current.MainWindow };
            LoadingOverlay.Visibility = Visibility.Visible;
            AddCustomStatusButton.IsEnabled = false;
            SaveSegmentsButton.IsEnabled = false;

            try
            {
                loading.Show();

                int totalDays = (toDate - fromDate).Days + 1;
                int processed = 0;

                for (var date = fromDate; date <= toDate; date = date.AddDays(1))
                {
                    // Пропускаем выходные
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    var leaveSegment = new FirebaseService.ExtendedPlannedSegment
                    {
                        UserId = uid,
                        Start = date.Date.AddHours(9),       // 09:00
                        End = date.Date.AddHours(18),        // 18:00
                        Status = status
                    };

                    await FirebaseService.SaveExtendedSegmentsAsync(
                        uid,
                        date,
                        new List<FirebaseService.ExtendedPlannedSegment> { leaveSegment }
                    );

                    processed++;
                    double progress = (double)processed / totalDays * 100;
                    loading.SetProgress(progress, $"Обработка {processed}/{totalDays}...");
                }

                await RedrawAllAsync();
                AppEvents.RaiseCalendarRefresh();

                loading.SetProgress(100, "Готово!");
                await Task.Delay(500);
            }
            finally
            {
                loading.Close();
                LoadingOverlay.Visibility = Visibility.Collapsed;
                AddCustomStatusButton.IsEnabled = true;
                SaveSegmentsButton.IsEnabled = true;
            }
        }

        private void EmployeeComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void SeedTestMonth_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedEmployeeId))
            {
                MessageBox.Show("Сначала выберите сотрудника из списка.");
                return;
            }

            // Например, текущий месяц и год
            var today = DateTime.Today;
            await FirebaseService.SeedFullMonthAsync(SelectedEmployeeId, today.Year, today.Month);

            MessageBox.Show($"Тестовые записи за {today:MMMM yyyy} для пользователя {SelectedEmployeeId} созданы.");
        }


        #endregion

        //private async void SaveButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (EmployeeComboBox.SelectedValue is string uid
        //     && datePicker.SelectedDate.HasValue
        //     && TimeSpan.TryParse(ShiftStartTextBox.Text, out var ts1)
        //     && TimeSpan.TryParse(ShiftEndTextBox.Text, out var ts2))
        //    {
        //        var shift = new FirebaseService.PlannedShift
        //        {
        //            UserId = uid,
        //            Date = datePicker.SelectedDate.Value,
        //            Start = datePicker.SelectedDate.Value.Date + ts1,
        //            End = datePicker.SelectedDate.Value.Date + ts2,
        //            LunchStart = TimeSpan.TryParse(LunchStartTextBox.Text, out var l1)
        //                         ? datePicker.SelectedDate.Value.Date + l1
        //                         : (DateTime?)null,
        //            LunchEnd = TimeSpan.TryParse(LunchEndTextBox.Text, out var l2)
        //                         ? datePicker.SelectedDate.Value.Date + l2
        //                         : (DateTime?)null
        //        };
        //        await FirebaseService.SavePlannedShiftAsync(shift);
        //        await RedrawAllAsync();
        //        MessageBox.Show("План смены сохранён.");
        //    }
        //    else
        //    {
        //        MessageBox.Show("Проверьте ввод времени.");
        //    }
        //}
    }
}