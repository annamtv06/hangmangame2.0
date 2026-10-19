using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hangman
{
    /// <summary>
    /// Загружает слова из словаря Ожегова (ozhegov.txt)
    /// Формат: VOCAB|BASEFORM|PHONGL|GRCLASSGL|STYLGL|DEF|ANTI|LEGLEXAM
    /// </summary>
    public static class WordProvider
    {
        public const int MinLength = 4;
        public const int MaxLength = 10;

        /// <summary>
        /// Загружает словарь и возвращает список слов, пригодных для игры.
        /// </summary>
        public static List<string> LoadWords(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Словарь не найден: {path}");

            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                if (rawLine.StartsWith("VOCAB|")) continue; // заголовок

                // Разделитель — вертикальная черта
                var parts = rawLine.Split('|');
                if (parts.Length < 2) continue;

                // VOCAB (первое поле) — то, что нам нужно
                string vocab = parts[0].Trim();

                if (!IsValidWord(vocab)) continue;

                // Заменяем ё на е, приводим к нижнему регистру
                string normalized = vocab.ToLowerInvariant().Replace('ё', 'е');

                words.Add(normalized);
            }

            if (words.Count == 0)
                throw new InvalidOperationException("Словарь пуст после фильтрации.");

            return words.ToList();
        }

        /// <summary>
        /// Проверяет, годится ли слово для игры.
        /// </summary>
        private static bool IsValidWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;

            // Длина
            if (word.Length < MinLength || word.Length > MaxLength) return false;

            // Не начинается с дефиса (в словаре есть -с, -тка, -то)
            if (word.StartsWith("-")) return false;

            // Не содержит дефисов и пробелов
            if (word.Contains('-') || word.Contains(' ')) return false;

            // Только буквы
            foreach (char c in word)
            {
                if (!char.IsLetter(c)) return false;
            }

            // Только кириллица
            foreach (char c in word)
            {
                char lower = char.ToLowerInvariant(c);
                if (lower < 'а' || lower > 'я')
                {
                    // Исключение для ё
                    if (lower != 'ё') return false;
                }
            }

            // Должны быть только строчные буквы (отсекаем аббревиатуры типа АТС, МХАТ)
            if (word.Any(char.IsUpper)) return false;

            return true;
        }

        /// <summary>
        /// Перемешивает слова и берёт первые N штук для игровой сессии.
        /// </summary>
        public static List<string> PickRandom(List<string> source, int count, Random rng)
        {
            if (source.Count <= count) return new List<string>(source);

            // Перемешивание Фишера–Йетса
            var copy = new List<string>(source);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy.Take(count).ToList();
        }
    }
}