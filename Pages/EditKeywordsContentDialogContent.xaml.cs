using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QStickerManager.Localization;
using QStickerManager.Stickers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace QStickerManager.Pages
{
    public sealed partial class EditKeywordsContentDialogContent : Page
    {
        private readonly HashSet<string> initialKeywords;
        private readonly ObservableCollection<string> editedKeywords;
        private readonly List<string> availableKeywords;

        public EditKeywordsContentDialogContent(
            IReadOnlyList<Sticker> stickers,
            IEnumerable<string> keywordSuggestions)
        {
            InitializeComponent();

            List<string> commonKeywords = stickers
                .Select(sticker => sticker.Keywords
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase))
                .Aggregate((common, next) =>
                {
                    common.IntersectWith(next);
                    return common;
                })
                .OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase)
                .ToList();

            initialKeywords = commonKeywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
            editedKeywords = [.. commonKeywords];
            availableKeywords = [.. keywordSuggestions];

            InstructionText.Text = stickers.Count == 1
                ? Localizer.Get("EditKeywords_OneInstruction")
                : Localizer.Get("EditKeywords_ManyInstruction");
            NoCommonKeywordsText.Visibility = commonKeywords.Count == 0 && stickers.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;

            KeywordBox.ItemsSource = editedKeywords;
            KeywordBox.SuggestedItemsSource = this.availableKeywords;
            KeywordBox.TokenItemAdding += KeywordBox_TokenItemAdding;
        }

        public IReadOnlyCollection<string> KeywordsToAdd
        {
            get => editedKeywords
                .Except(initialKeywords, StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> KeywordsToRemove
        {
            get => initialKeywords
                .Except(editedKeywords, StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private void KeywordBox_TokenItemAdding(
            TokenizingTextBox sender,
            TokenItemAddingEventArgs args)
        {
            string input = args.TokenText.Trim();
            if (string.IsNullOrWhiteSpace(input)
                || editedKeywords.Any(existing =>
                    string.Equals(existing, input, StringComparison.OrdinalIgnoreCase)))
            {
                args.Cancel = true;
                return;
            }

            args.Item = availableKeywords.FirstOrDefault(existing =>
                    string.Equals(existing, input, StringComparison.OrdinalIgnoreCase))
                ?? input;
        }
    }
}
