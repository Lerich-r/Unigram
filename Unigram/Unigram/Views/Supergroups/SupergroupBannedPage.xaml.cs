﻿using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupBannedPage : HostedPage, ISupergroupDelegate, ISearchablePage
    {
        public SupergroupBannedViewModel ViewModel => DataContext as SupergroupBannedViewModel;

        public SupergroupBannedPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ChannelBlockedUsers;
        }

        public void Search()
        {
            SearchField.StartBringIntoView();
            SearchField.Focus(FocusState.Keyboard);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var member = e.ClickedItem as ChatMember;
            if (member == null)
            {
                return;
            }

            ViewModel.NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), state: NavigationState.GetChatMember(chat.Id, member.MemberId));
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = element.Tag as ChatMember;

            flyout.CreateFlyoutItem(ViewModel.MemberUnbanCommand, member, Strings.Resources.Unban);

            args.ShowAt(flyout, element);
        }

        #endregion

        #region Binding

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            AddNewPanel.Visibility = group.CanRestrictMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.Resources.NoBlockedChannel : Strings.Resources.NoBlockedGroup;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo) { }
        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as UserCell;
            var member = args.Item as ChatMember;

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;

            content.UpdateSupergroupBanned(ViewModel.ProtoService, args, OnContainerContentChanging);
        }

        #endregion
    }
}
