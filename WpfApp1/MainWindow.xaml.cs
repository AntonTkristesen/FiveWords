using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FiveWordsFiveLetters
{
    public partial class MainWindow : Window
    {
        private const int CombinationSize = 5; // Size of the combination to search for
        private string selectedFilePath = "ArrayOfWords.txt"; // Default file

        public MainWindow()
        {
            InitializeComponent();
            FilePathTextBox.Text = selectedFilePath; // Show default file path
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                FilePathTextBox.Text = selectedFilePath; // Update the UI with the selected file path
            }
        }

        private async void StartSearch_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();

            Stopwatch stopwatch = Stopwatch.StartNew();

            var wordsWithBitmasks = LoadAndFilterWords();
            var totalWords = wordsWithBitmasks.Count;
            var allCombinations = new ConcurrentBag<string>();
            int completedIterations = 0;

            await Task.Run(() =>
            {
                Parallel.For(0, totalWords, i =>
                {
                    var selectedIndices = new List<int> { i };
                    FindCombinations(wordsWithBitmasks.Select(x => x.Word).ToList(),
                                     wordsWithBitmasks.Select(x => x.Bitmask).ToList(),
                                     selectedIndices,
                                     wordsWithBitmasks[i].Bitmask,
                                     1, i + 1, allCombinations, totalWords);

                    if (System.Threading.Interlocked.Increment(ref completedIterations) % 100 == 0)
                    {
                        ReportProgress(completedIterations, totalWords);
                    }
                });
            });

            stopwatch.Stop();
            DisplayResults(allCombinations, stopwatch.ElapsedMilliseconds);
        }

        private void ResetUI()
        {
            ResultsTextBox.Text = "";
            ProgressText.Text = "";
            CombinationsCountText.Text = "";
            TimeTakenText.Text = "";
            ProgressBar.Value = 0;
        }

        private List<(string Word, int Bitmask)> LoadAndFilterWords()
        {
            return File.ReadAllLines(selectedFilePath)
                       .Select(word => (Word: word, Bitmask: GetWordBitmask(word)))
                       .Where(x => x.Word.Length == 5 && x.Word.Distinct().Count() == 5)
                       .ToList();
        }

        private void ReportProgress(int completedIterations, int totalWords)
        {
            int progressPercentage = (completedIterations * 100) / totalWords;

            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = progressPercentage;
                ProgressText.Text = $"{progressPercentage}% Complete";
            });
        }

        private void FindCombinations(List<string> words, List<int> wordBitmasks, List<int> selectedIndices, int usedBitmask, int selectedCount, int startIndex, ConcurrentBag<string> allCombinations, int totalWords)
        {
            if (selectedCount == CombinationSize)
            {
                var combination = string.Join(", ", selectedIndices.Select(index => words[index]));
                allCombinations.Add(combination);
                return;
            }

            for (int i = startIndex; i < totalWords; i++)
            {
                int wordBitmask = wordBitmasks[i];

                if ((usedBitmask & wordBitmask) != 0) continue;

                selectedIndices.Add(i);
                FindCombinations(words, wordBitmasks, selectedIndices, usedBitmask | wordBitmask, selectedCount + 1, i + 1, allCombinations, totalWords);
                selectedIndices.RemoveAt(selectedCount);
            }
        }

        private int GetWordBitmask(string word)
        {
            return word.Aggregate(0, (bitmask, c) => bitmask | (1 << (c - 'a')));
        }

        private void DisplayResults(ConcurrentBag<string> allCombinations, long elapsedMilliseconds)
        {
            ResultsTextBox.Text = allCombinations.Count > 0
                ? string.Join(Environment.NewLine, allCombinations)
                : "No valid combination of words found.";
            CombinationsCountText.Text = $"Total combinations found: {allCombinations.Count}";
            TimeTakenText.Text = $"Time taken: {elapsedMilliseconds} ms";
        }

        private void ExportResults_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "Results.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, ResultsTextBox.Text);
                    MessageBox.Show("Results exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while exporting the results: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
