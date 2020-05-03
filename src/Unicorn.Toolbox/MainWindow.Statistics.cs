﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Unicorn.Toolbox.Analysis;
using Unicorn.Toolbox.Analysis.Filtering;
using Unicorn.Toolbox.Visualization;

namespace Unicorn.Toolbox
{
    public partial class MainWindow
    {
        private Analyzer analyzer;

        private void LoadTestsAssembly(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Unicorn tests assemblies|*.dll"
            };
            openFileDialog.ShowDialog();

            groupBoxVisualization.IsEnabled = true;
            groupBoxVisualizationStateTemp = true;
            var assemblyFile = openFileDialog.FileName;

            if (string.IsNullOrEmpty(assemblyFile))
            {
                return;
            }

            gridFilters.IsEnabled = true;

            analyzer = new Analyzer(assemblyFile);
            analyzer.GetTestsStatistics();

            var statusLine = $"Assembly: {analyzer.AssemblyFileName} ({analyzer.TestsAssemblyName})    |    " + analyzer.Data.ToString();
            textBoxStatistics.Text = statusLine;

            FillFiltersFrom(analyzer.Data);
            ShowAll();
            checkBoxShowHide.IsChecked = true;
        }

        private void FillFiltersFrom(AutomationData data)
        {
            FillGrid(gridFeatures, data.UniqueFeatures);
            FillGrid(gridCategories, data.UniqueCategories);
            FillGrid(gridAuthors, data.UniqueAuthors);
        }

        private void FillGrid(Grid grid, HashSet<string> items)
        {
            var sortedItems = items.OrderBy(s => s).ToList();

            grid.Children.Clear();
            grid.RowDefinitions.Clear();

            grid.Height = (sortedItems.Count + 2) * 20;

            for (int i = 0; i < sortedItems.Count + 2; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
            }

            int index = 2;

            foreach (var item in sortedItems)
            {
                var itemCheckbox = new CheckBox
                {
                    Content = item,
                    IsChecked = true
                };

                grid.Children.Add(itemCheckbox);
                Grid.SetRow(itemCheckbox, index++);
            }
        }

        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            var features = from CheckBox cBox in gridFeatures.Children where cBox.IsChecked.Value select (string)cBox.Content;
            var categories = from CheckBox cBox in gridCategories.Children where cBox.IsChecked.Value select (string)cBox.Content;
            var authors = from CheckBox cBox in gridAuthors.Children where cBox.IsChecked.Value select (string)cBox.Content;

            analyzer.Data.ClearFilters();
            analyzer.Data.FilterBy(new FeaturesFilter(features));
            analyzer.Data.FilterBy(new CategoriesFilter(categories));
            analyzer.Data.FilterBy(new AuthorsFilter(authors));

            if (checkOnlyDisabledTests.IsChecked.Value)
            {
                analyzer.Data.FilterBy(new OnlyDisabledFilter());
            }

            if (checkOnlyEnabledTests.IsChecked.Value)
            {
                analyzer.Data.FilterBy(new OnlyEnabledFilter());
            }

            gridResults.ItemsSource = analyzer.Data.FilteredInfo;

            var foundTestsCount = analyzer.Data.FilteredInfo.SelectMany(si => si.TestsInfos).Count();

            var filterText = new StringBuilder()
                .AppendFormat("Found {0} tests. Filters:\nFeatures[{1}]\n", foundTestsCount, string.Join(",", features))
                .AppendFormat("Categories[{0}]\n", string.Join(", ", categories))
                .AppendFormat("Authors[{0}]", string.Join(", ", authors));

            textBoxCurrentFilter.Text = filterText.ToString();
        }

        private void ClickCellWithSuite(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                string testSuiteName = (sender as TextBlock).Text;

                var preview = new WindowTestPreview();
                preview.Title += testSuiteName;
                preview.ShowActivated = false;
                preview.Show();
                preview.gridResults.ItemsSource = analyzer.Data.FilteredInfo.First(s => s.Name.Equals(testSuiteName)).TestsInfos;
            }
        }

        private void ShowAllClick(object sender, RoutedEventArgs e) => ShowAll();

        private void FilterOnlyDisabledTests(object sender, RoutedEventArgs e) =>
            checkOnlyEnabledTests.IsChecked = false;

        private void FilterOnlyEnabledTests(object sender, RoutedEventArgs e) =>
            checkOnlyDisabledTests.IsChecked = false;

        private void UpdateRunTagsText(object sender, RoutedEventArgs e)
        {
            var runTags = new HashSet<string>();

            foreach (var child in gridModules.Children)
            {
                var checkbox = child as CheckBox;

                if (checkbox.IsChecked.Value)
                {
                    runTags.UnionWith(_coverage.Specs.Modules
                        .First(m => m.Name.Equals(checkbox.Content.ToString(), StringComparison.InvariantCultureIgnoreCase))
                        .Features);
                }
            }

            textBoxRunTags.Text = "#" + string.Join(" #", runTags);
        }

        private void ShowAll()
        {
            analyzer.Data.ClearFilters();
            gridResults.ItemsSource = analyzer.Data.FilteredInfo;
            textBoxCurrentFilter.Text = string.Empty;
        }

        private FilterType GetFilter()
        {
            if (tabFeaures.IsSelected)
            {
                return FilterType.Feature;
            }
            else if (tabCategories.IsSelected)
            {
                return FilterType.Category;
            }
            else
            {
                return FilterType.Author;
            }
        }

        private void VisualizeStatistics()
        {
            var filter = GetFilter();

            var visualization = GetVisualizationWindow($"Overall tests statistics: {filter}");
            visualization.Show();

            if (checkBoxModern.IsChecked.HasValue && checkBoxModern.IsChecked.Value)
            {
                new VisualizerCircles(visualization.canvasVisualization, GetPalette()).VisualizeAutomationData(analyzer.Data, filter);
            }
            else
            {
                new VisualizerBars(visualization.canvasVisualization, GetPalette()).VisualizeAutomationData(analyzer.Data, filter);
                InjectExportToVisualization(visualization);
            }
        }

        private void HideAll(object sender, RoutedEventArgs e)
        {
            var activeGrid = GetActiveGrid();

            foreach (var child in activeGrid.Children)
            {
                ((CheckBox)child).IsChecked = false;
            }
        }

        private void ShowAll(object sender, RoutedEventArgs e)
        {
            var activeGrid = GetActiveGrid();

            foreach (var child in activeGrid.Children)
            {
                ((CheckBox)child).IsChecked = true;
            }
        }

        private Grid GetActiveGrid()
        {
            if (tabFeaures.IsSelected)
            {
                return gridFeatures;
            }
            else if (tabCategories.IsSelected)
            {
                return gridCategories;
            }
            else
            {
                return gridAuthors;
            }
        }
    }
}
