﻿using System.Linq;
using Unicorn.Toolbox.Models.Stats;
using Unicorn.Toolbox.Models.Stats.Filtering;
using Unicorn.Toolbox.ViewModels;

namespace Unicorn.Toolbox.Commands
{
    public class ApplyFilterCommand : CommandBase
    {
        private readonly StatsViewModel _viewModel;
        private readonly StatsCollector _analyzer;

        public ApplyFilterCommand(StatsViewModel viewModel, StatsCollector analyzer)
        {
            _viewModel = viewModel;
            _analyzer = analyzer;
        }

        public override void Execute(object parameter)
        {
            _analyzer.Data.ClearFilters();
            _analyzer.Data.FilterBy(new TagsFilter(_viewModel.Tags.Where(t => t.Selected).Select(t => t.Name)));
            _analyzer.Data.FilterBy(new CategoriesFilter(_viewModel.Categories.Where(c => c.Selected).Select(c => c.Name)));
            _analyzer.Data.FilterBy(new AuthorsFilter(_viewModel.Authors.Where(a => a.Selected).Select(a => a.Name)));

            if (_viewModel.FilterOnlyDisabledTests)
            {
                _analyzer.Data.FilterBy(new OnlyDisabledFilter());
            }

            if (_viewModel.FilterOnlyEnabledTests)
            {
                _analyzer.Data.FilterBy(new OnlyEnabledFilter());
            }

            _viewModel.ApplyFilteredData();
        }
    }
}
