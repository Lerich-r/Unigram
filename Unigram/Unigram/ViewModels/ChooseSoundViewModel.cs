﻿using Rg.DiffUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ChooseSoundViewModel : TLViewModelBase, IHandle<UpdateSavedNotificationSounds>
    {
        public ChooseSoundViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<NotificationSoundViewModel>(new NotificationSoundDiffHandler());

            UploadCommand = new RelayCommand(Upload);
        }

        public DiffObservableCollection<NotificationSoundViewModel> Items { get; }

        public bool CanUploadMore => Items.Count < CacheService.Options.NotificationSoundCountMax;

        public async void Handle(UpdateSavedNotificationSounds update)
        {
            var response = await ProtoService.SendAsync(new GetSavedNotificationSounds());
            if (response is NotificationSounds sounds)
            {
                BeginOnUIThread(() =>
                {
                    Items.ReplaceDiff(sounds.NotificationSoundsValue.Select(x => new NotificationSoundViewModel(this, x, false)));
                    RaisePropertyChanged(nameof(CanUploadMore));
                });
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ProtoService.SendAsync(new GetSavedNotificationSounds());
            if (response is NotificationSounds sounds && parameter is long selected)
            {
                Items.ReplaceDiff(sounds.NotificationSoundsValue.Select(x => new NotificationSoundViewModel(this, x, x.Id == selected)));
                RaisePropertyChanged(nameof(CanUploadMore));
            }

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void UpdateFile(object sender, File file)
        {
            if (sender is NotificationSoundViewModel notificationSound && notificationSound.IsSelected)
            {
                SoundEffects.Play(file);
            }
        }

        public RelayCommand UploadCommand { get; }
        private async void Upload()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.FileTypeFilter.Add(".mp3");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var properties = await file.GetBasicPropertiesAsync();
                    if ((long)properties.Size > CacheService.Options.NotificationSoundSizeMax)
                    {
                        // TODO: ...
                        return;
                    }

                    var music = await file.Properties.GetMusicPropertiesAsync();
                    if (music.Duration.TotalSeconds > CacheService.Options.NotificationSoundDurationMax)
                    {
                        // TODO: ...
                        return;
                    }

                    ProtoService.Send(new AddSavedNotificationSound(await file.ToGeneratedAsync()));
                }
            }
            catch { }

        }
    }

    public class NotificationSoundViewModel : BindableBase
    {
        private readonly ChooseSoundViewModel _parent;

        private readonly NotificationSound _notificationSound;
        private string _soundToken;

        public NotificationSoundViewModel(ChooseSoundViewModel parent, NotificationSound notificationSound, bool selected)
        {
            _parent = parent;

            _notificationSound = notificationSound;
            _isSelected = selected;
        }

        public NotificationSound Get()
        {
            return _notificationSound;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetSelected(value);
        }

        private void SetSelected(bool value)
        {
            if (Set(ref _isSelected, value, nameof(IsSelected)) && _isSelected)
            {
                var file = _notificationSound.Sound;
                if (file.Local.IsFileExisting())
                {
                    SoundEffects.Play(file);
                }
                else
                {
                    UpdateManager.Subscribe(this, _parent.ProtoService, file, ref _soundToken, _parent.UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsFileExisting())
                    {
                        _parent.ProtoService.DownloadFile(file.Id, 16);
                    }
                }
            }
        }

        /// <summary>
        /// File containing the sound.
        /// </summary>
        public File Sound => _notificationSound.Sound;

        /// <summary>
        /// Arbitrary data, defined while the sound was uploaded.
        /// </summary>
        public string Data => _notificationSound.Data;

        /// <summary>
        /// Title of the notification sound.
        /// </summary>
        public string Title => _notificationSound.Title;

        /// <summary>
        /// Point in time (Unix timestamp) when the sound was created.
        /// </summary>
        public int Date => _notificationSound.Date;

        /// <summary>
        /// Duration of the sound, in seconds.
        /// </summary>
        public int Duration => _notificationSound.Duration;

        /// <summary>
        /// Unique identifier of the notification sound.
        /// </summary>
        public long Id => _notificationSound.Id;
    }

    public class NotificationSoundDiffHandler : IDiffHandler<NotificationSoundViewModel>
    {
        public bool CompareItems(NotificationSoundViewModel oldItem, NotificationSoundViewModel newItem)
        {
            return oldItem?.Id == newItem?.Id;
        }

        public void UpdateItem(NotificationSoundViewModel oldItem, NotificationSoundViewModel newItem)
        {
            // ...
        }
    }
}
