﻿using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedMembersPage : ChatSharedMediaPageBase
    {
        public ChatSharedMembersPage()
        {
            InitializeComponent();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatMember member)
            {
                ViewModel.NavigationService.NavigateToSender(member.MemberId);
            }
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = element.Tag as ChatMember;

            var chat = ViewModel.Chat;
            if (chat == null || member == null)
            {
                return;
            }

            ChatMemberStatus status = null;
            if (chat.Type is ChatTypeBasicGroup basic)
            {
                status = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId)?.Status;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                status = ViewModel.ProtoService.GetSupergroup(super.SupergroupId)?.Status;
            }

            if (status == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                flyout.CreateFlyoutItem(MemberPromote_Loaded, ViewModel.MemberPromoteCommand, chat.Type, status, member, Strings.Resources.SetAsAdmin, new FontIcon { Glyph = Icons.Star });
                flyout.CreateFlyoutItem(MemberRestrict_Loaded, ViewModel.MemberRestrictCommand, chat.Type, status, member, Strings.Resources.KickFromSupergroup, new FontIcon { Glyph = Icons.LockClosed });
            }

            flyout.CreateFlyoutItem(MemberRemove_Loaded, ViewModel.MemberRemoveCommand, chat.Type, status, member, Strings.Resources.KickFromGroup, new FontIcon { Glyph = Icons.Block });

            args.ShowAt(flyout, element);
        }

        private bool MemberPromote_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.CacheService.Options.MyId))
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPromoteMembers;
        }

        private bool MemberRestrict_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusRestricted || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.CacheService.Options.MyId))
            {
                return false;
            }

            if (chatType is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanRestrictMembers;
        }

        private bool MemberRemove_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.CacheService.Options.MyId))
            {
                return false;
            }

            if (chatType is ChatTypeBasicGroup && status is ChatMemberStatusAdministrator)
            {
                return member.InviterUserId == ViewModel.CacheService.Options.MyId;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanRestrictMembers;
        }

        #endregion

        #region Recycle

        protected override void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Member_ContextRequested;

                if (sender.ItemTemplateSelector == null)
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                }
            }

            if (sender.ItemTemplateSelector != null)
            {
                args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
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
            if (content != null)
            {
                content.UpdateChatSharedMembers(ViewModel.ProtoService, args, OnContainerContentChanging);
            }
        }

        #endregion
    }
}
