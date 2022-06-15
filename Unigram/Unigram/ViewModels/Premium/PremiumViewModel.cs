﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Premium.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Premium
{
    public class PremiumViewModel : TLViewModelBase
    {
        public PremiumViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Limits = new MvxObservableCollection<PremiumLimit>();
            Features = new MvxObservableCollection<PremiumFeature>();
        }

        private InternalLinkType _paymentLink;
        public InternalLinkType PaymentLink
        {
            get => _paymentLink;
            set => Set(ref _paymentLink, value);
        }

        public MvxObservableCollection<PremiumLimit> Limits { get; private set; }

        public MvxObservableCollection<PremiumFeature> Features { get; private set; }

        public Dictionary<Type, Animation> Animations { get; private set; }

        private long _monthlyAmount;
        public long MonthlyAmount
        {
            get => _monthlyAmount;
            set => Set(ref _monthlyAmount, value);
        }

        private string _currency;
        public string Currency
        {
            get => _currency;
            set => Set(ref _currency, value);
        }

        private FormattedText _state;
        public FormattedText State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        private bool _canPurchase;
        public bool CanPurchase
        {
            get => _canPurchase;
            set => Set(ref _canPurchase, value);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState _)
        {
            var features = await ProtoService.SendAsync(new GetPremiumFeatures(new PremiumSourceSettings())) as PremiumFeatures;
            var state = await ProtoService.SendAsync(new GetPremiumState()) as PremiumState;

            if (features == null || state == null)
            {
                return;
            }

            var appIcons = features.Features.FirstOrDefault(x => x is PremiumFeatureAppIcons);
            if (appIcons != null)
            {
                features.Features.Remove(appIcons);
            }

            var archivedChats = features.Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedArchivedChatCount);
            if (archivedChats != null)
            {
                features.Limits.Remove(archivedChats);
            }

            features.Limits.Add(new PremiumLimit(new PremiumLimitTypeConnectedAccounts(), 3, 4));

            PaymentLink = features.PaymentLink;
            Limits.ReplaceWith(features.Limits);
            Features.ReplaceWith(features.Features);

            Animations = state.Animations.ToDictionary(x => x.Feature.GetType(), y => y.Animation);

            MonthlyAmount = state.MonthlyAmount;
            Currency = state.Currency;
            State = state.State;

            CanPurchase = features.PaymentLink != null
                && ProtoService.IsPremiumAvailable;
        }

        public string PremiumPreviewLimitsDescription
        {
            get
            {
                var channels = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeSupergroupCount)?.PremiumValue ?? 0;
                var folders = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeChatFilterCount)?.PremiumValue ?? 0;
                var pinned = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedChatCount)?.PremiumValue ?? 0;
                var links = Limits.FirstOrDefault(x => x.Type is PremiumLimitTypeCreatedPublicChatCount)?.PremiumValue ?? 0;
                var accounts = 4;

                return string.Format(Strings.Resources.PremiumPreviewLimitsDescription, channels, folders, pinned, links, accounts);
            }
        }

        public async void Open(PremiumFeature feature)
        {
            if (feature is PremiumFeatureIncreasedLimits)
            {
                await new LimitsPopup(Limits).ShowQueuedAsync();
            }
            else
            {
                await new FeaturesPopup(ProtoService, Features, Animations, feature).ShowQueuedAsync();
            }
        }

        public async void Purchase()
        {
            if (PaymentLink != null)
            {
                MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, PaymentLink);

                if (PaymentLink is InternalLinkTypeBotStart botStart)
                {
                    var chat = await ProtoService.SendAsync(new SearchPublicChat(botStart.BotUsername)) as Chat;
                    if (chat != null && chat.Type is ChatTypePrivate privata)
                    {
                        ProtoService.Send(new SendBotStartMessage(privata.UserId, chat.Id, botStart.StartParameter));
                    }
                }
            }
        }
    }
}
