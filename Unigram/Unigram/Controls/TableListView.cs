﻿using System;
using System.Collections;
using System.Collections.Specialized;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Controls
{
    public class TableListView : SelectListView
    {
        private INotifyCollectionChanged _itemsSource;

        public TableListView()
        {
            DefaultStyleKey = typeof(TableListView);

            ContainerContentChanging += OnContainerContentChanging;
            //RegisterPropertyChangedCallback(ItemsSourceProperty, OnItemsSourceChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                if (args.ItemIndex == 0)
                {
                    content.CornerRadius = new CornerRadius(4, 4, 0, 0);
                    content.BorderThickness = new Thickness(1);
                }
                else if (ItemsSource is IList list && args.ItemIndex == list.Count - 1)
                {
                    content.CornerRadius = new CornerRadius(0, 0, 4, 4);
                    content.BorderThickness = new Thickness(1, 0, 1, 1);
                }
                else
                {
                    content.CornerRadius = new CornerRadius(0);
                    content.BorderThickness = new Thickness(1, 0, 1, 1);
                }
            }
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_itemsSource is INotifyCollectionChanged oldValue)
            {
                _itemsSource = null;
                oldValue.CollectionChanged -= OnCollectionChanged;
            }

            if (ItemsSource is INotifyCollectionChanged newValue)
            {
                _itemsSource = newValue;
                newValue.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var list = sender as IList;
            if (list == null)
            {
                return;
            }

            var stack = ItemsPanelRoot as ItemsStackPanel;
            if (stack == null)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && (e.NewStartingIndex == 0 || e.NewStartingIndex == list.Count - 1))
            {
                if (stack.FirstCacheIndex <= e.NewStartingIndex && stack.LastCacheIndex >= e.NewStartingIndex)
                {

                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TableListViewItem();
        }
    }

    public class TableListViewItem : TextListViewItem
    {
        private ListViewItemPresenter Root;

        protected override void OnApplyTemplate()
        {
            Root = GetTemplateChild(nameof(Root)) as ListViewItemPresenter; 
            base.OnApplyTemplate();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Root == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            Root.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, 1000), finalSize.Height));
            return finalSize;
        }
    }
}
