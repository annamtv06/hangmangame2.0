using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Hangman
{
    public partial class MainWindow : Window
    {
        // ================= СЛОВАРЬ =================
        // Все слова, загруженные из ozhegov.txt
        private List<string> _allWords = new List<string>();

        private const string Alphabet = "абвгдежзийклмнопрстуфхцчшщъыьэюя";
        private const int MaxWrong = 6;
        private const int GameDurationSeconds = 300; // 5 минут
        private const int WordsPerSession = 100;     // сколько слов берём на сессию

        // ================= СОСТОЯНИЕ ИГРОКА =================
        private class PlayerState
        {
            public string Word = "";
            public List<string> WordPool = new List<string>();
            public HashSet<char> Guessed = new HashSet<char>();
            public int Wrong;
            public int SolvedCount;
        }

        private PlayerState _p1 = new PlayerState();
        private PlayerState _p2 = new PlayerState();
        private bool _twoPlayers;
        private DispatcherTimer _timer;
        private int _secondsLeft;
        private readonly Random _rng = new Random();

        // ================= ЦВЕТА ТЕМЫ =================
        private Brush _bgColor = Brushes.WhiteSmoke;
        private Brush _textColor = Brushes.Black;
        private Brush _accentColor = Brushes.SteelBlue;

        public MainWindow()
        {
            LoadDictionary();          // ← сначала словарь
            InitializeComponent();
            ApplyTheme("Светлая");
        }

        // ================= ЗАГРУЗКА СЛОВАРЯ =================
        private void LoadDictionary()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ozhegov.txt");

            try
            {
                _allWords = WordProvider.LoadWords(path);
                System.Diagnostics.Debug.WriteLine($"Загружено слов: {_allWords.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось загрузить словарь: {ex.Message}\n\n" +
                    $"Ожидался файл: {path}\n\n" +
                    "Будут использоваться слова по умолчанию.",
                    "Ошибка словаря",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // Fallback — если словарь не найден
                _allWords = new List<string>
                {
                    "тигр", "слон", "жираф", "пингвин", "медведь",
                    "лиса", "волк", "заяц", "кот", "собака",
                    "москва", "париж", "лондон", "берлин", "токио"
                };
            }
        }

        // ================= ТЕМА =================
        private void ApplyTheme(string theme)
        {
            if (theme == "Тёмная")
            {
                _bgColor = new SolidColorBrush(Color.FromRgb(30, 30, 40));
                _textColor = Brushes.White;
                _accentColor = new SolidColorBrush(Color.FromRgb(120, 200, 255));
            }
            else
            {
                _bgColor = Brushes.WhiteSmoke;
                _textColor = Brushes.Black;
                _accentColor = new SolidColorBrush(Color.FromRgb(30, 100, 200));
            }

            RootGrid.Background = _bgColor;
            MenuScreen.Background = _bgColor;
            GameScreen.Background = _bgColor;

            foreach (var tb in FindVisualChildren<TextBlock>(this))
                tb.Foreground = _textColor;

            TimerText.Foreground = _accentColor;
            Score1.Foreground = _accentColor;
            Score2.Foreground = _accentColor;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var c in FindVisualChildren<T>(child)) yield return c;
            }
        }

        // ================= СТАРТ ИГРЫ =================
        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            _twoPlayers = ((ComboBoxItem)PlayersBox.SelectedItem).Content.ToString() == "2";
            ApplyTheme(((ComboBoxItem)ThemeBox.SelectedItem).Content.ToString());

            MenuScreen.Visibility = Visibility.Collapsed;
            GameScreen.Visibility = Visibility.Visible;

            if (_twoPlayers)
            {
                Splitter.Width = new GridLength(2);
                Col2.Width = new GridLength(1, GridUnitType.Star);
                Panel2Border.Visibility = Visibility.Visible;
            }
            else
            {
                Splitter.Width = new GridLength(0);
                Col2.Width = new GridLength(0);
                Panel2Border.Visibility = Visibility.Collapsed;
            }

            // Берём случайные слова на сессию
            var sessionWords = WordProvider.PickRandom(_allWords, WordsPerSession, _rng);

            _p1 = new PlayerState { WordPool = new List<string>(sessionWords) };
            _p2 = new PlayerState { WordPool = _twoPlayers ? new List<string>(sessionWords) : new List<string>() };

            NewWord(_p1);
            if (_twoPlayers) NewWord(_p2);

            BuildKeyboard(Keyboard1, OnGuessP1);
            if (_twoPlayers) BuildKeyboard(Keyboard2, OnGuessP2);

            UpdateUI();
            StartTimer();
        }

        private void BuildKeyboard(UniformGrid grid, Action<char> handler)
        {
            grid.Children.Clear();
            foreach (char c in Alphabet)
            {
                var btn = new Button
                {
                    Content = c.ToString().ToUpper(),
                    Tag = c,
                    Margin = new Thickness(2),
                    FontSize = 14,
                    MinWidth = 30,
                    MinHeight = 30
                };
                btn.Click += (s, e) => handler((char)((Button)s).Tag);
                grid.Children.Add(btn);
            }
        }

        private void OnGuessP1(char c) { Guess(_p1, c); UpdateUI(); }
        private void OnGuessP2(char c) { Guess(_p2, c); UpdateUI(); }

        private void Guess(PlayerState p, char c)
        {
            if (p.Guessed.Contains(c)) return;
            p.Guessed.Add(c);
            if (!p.Word.Contains(c))
            {
                p.Wrong++;
                if (p.Wrong >= MaxWrong)
                {
                    p.Wrong = 0;
                    NewWord(p);
                }
            }
            else if (p.Word.All(ch => p.Guessed.Contains(ch)))
            {
                p.SolvedCount++;
                p.Wrong = 0;
                NewWord(p);
            }
        }

        private void NewWord(PlayerState p)
        {
            if (p.WordPool.Count == 0)
            {
                // Если слова закончились — добираем из общего списка
                p.WordPool = WordProvider.PickRandom(_allWords, 50, _rng);
            }

            int idx = _rng.Next(p.WordPool.Count);
            p.Word = p.WordPool[idx];
            p.WordPool.RemoveAt(idx);

            p.Guessed.Clear();
            p.Wrong = 0;
        }

        // ================= ОТРИСОВКА =================
        private void UpdateUI()
        {
            UpdatePlayer(_p1, Canvas1, Word1, Wrong1, Score1, Keyboard1);
            if (_twoPlayers)
                UpdatePlayer(_p2, Canvas2, Word2, Wrong2, Score2, Keyboard2);
        }

        private void UpdatePlayer(PlayerState p, Canvas canvas, TextBlock word, TextBlock wrong,
                                  TextBlock score, UniformGrid keyboard)
        {
            word.Text = string.Join(" ", p.Word.Select(ch => p.Guessed.Contains(ch) ? ch : '_'));
            wrong.Text = $"Ошибки: {p.Wrong}/{MaxWrong}";
            score.Text = $"Угадано слов: {p.SolvedCount}";
            DrawHangman(canvas, p.Wrong);

            foreach (Button btn in keyboard.Children)
            {
                char c = (char)btn.Tag;
                if (p.Guessed.Contains(c))
                {
                    btn.IsEnabled = false;
                    btn.Background = p.Word.Contains(c) ? Brushes.LightGreen : Brushes.LightCoral;
                }
                else
                {
                    btn.IsEnabled = true;
                    btn.ClearValue(Button.BackgroundProperty);
                }
            }
        }

        private void DrawHangman(Canvas canvas, int wrong)
        {
            canvas.Children.Clear();
            var stroke = _textColor;
            const double t = 3;

            canvas.Children.Add(new Line { X1 = 20, Y1 = 180, X2 = 180, Y2 = 180, Stroke = stroke, StrokeThickness = t });
            canvas.Children.Add(new Line { X1 = 60, Y1 = 180, X2 = 60, Y2 = 20, Stroke = stroke, StrokeThickness = t });
            canvas.Children.Add(new Line { X1 = 60, Y1 = 20, X2 = 140, Y2 = 20, Stroke = stroke, StrokeThickness = t });
            canvas.Children.Add(new Line { X1 = 140, Y1 = 20, X2 = 140, Y2 = 50, Stroke = stroke, StrokeThickness = t });

            if (wrong >= 1)
                canvas.Children.Add(new Ellipse { Width = 40, Height = 40, Stroke = stroke, StrokeThickness = t, Margin = new Thickness(120, 50, 0, 0) });
            if (wrong >= 2)
                canvas.Children.Add(new Line { X1 = 140, Y1 = 90, X2 = 140, Y2 = 140, Stroke = stroke, StrokeThickness = t });
            if (wrong >= 3)
                canvas.Children.Add(new Line { X1 = 140, Y1 = 100, X2 = 115, Y2 = 125, Stroke = stroke, StrokeThickness = t });
            if (wrong >= 4)
                canvas.Children.Add(new Line { X1 = 140, Y1 = 100, X2 = 165, Y2 = 125, Stroke = stroke, StrokeThickness = t });
            if (wrong >= 5)
                canvas.Children.Add(new Line { X1 = 140, Y1 = 140, X2 = 120, Y2 = 175, Stroke = stroke, StrokeThickness = t });
            if (wrong >= 6)
                canvas.Children.Add(new Line { X1 = 140, Y1 = 140, X2 = 160, Y2 = 175, Stroke = stroke, StrokeThickness = t });
        }

        // ================= ТАЙМЕР =================
        private void StartTimer()
        {
            _secondsLeft = GameDurationSeconds;
            UpdateTimerText();

            if (_timer != null) _timer.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                _secondsLeft--;
                UpdateTimerText();
                if (_secondsLeft <= 0)
                {
                    _timer.Stop();
                    EndGame();
                }
            };
            _timer.Start();
        }

        private void UpdateTimerText()
        {
            TimerText.Text = $"{_secondsLeft / 60:00}:{_secondsLeft % 60:00}";
        }

        private void EndGame()
        {
            if (_twoPlayers)
            {
                string result;
                if (_p1.SolvedCount > _p2.SolvedCount)
                    result = $"Победил Игрок 1!\n\nИгрок 1: {_p1.SolvedCount}\nИгрок 2: {_p2.SolvedCount}";
                else if (_p2.SolvedCount > _p1.SolvedCount)
                    result = $"Победил Игрок 2!\n\nИгрок 1: {_p1.SolvedCount}\nИгрок 2: {_p2.SolvedCount}";
                else
                    result = $"Ничья!\n\nОба угадали по {_p1.SolvedCount} слов";

                MessageBox.Show(result, "Время вышло!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Время вышло!\nУгадано слов: {_p1.SolvedCount}",
                    "Игра окончена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BackToMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null) _timer.Stop();
            GameScreen.Visibility = Visibility.Collapsed;
            MenuScreen.Visibility = Visibility.Visible;
        }
    }
}