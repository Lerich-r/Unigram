﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        MessageWithOwner CurrentItem { get; }

        double PlaybackRate { get; set; }

        double Volume { get; set; }

        void Pause();
        void Play();

        void MoveNext();
        void MovePrevious();

        void Seek(TimeSpan span);

        void Clear();

        void Play(MessageWithOwner message, long threadId = 0);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        MediaPlaybackState PlaybackState { get; }



        bool? IsRepeatEnabled { get; set; }
        bool IsShuffleEnabled { get; set; }
        bool IsReversed { get; set; }


        bool IsSupportedPlaybackRateRange(double min, double max);



        event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        event TypedEventHandler<IPlaybackService, object> PositionChanged;
        event EventHandler PlaylistChanged;
    }

    public class PlaybackService : BindableBase, IPlaybackService
    {
        private readonly ISettingsService _settingsService;

        private readonly MediaPlayer _mediaPlayer;

        private readonly SystemMediaTransportControls _transport;

        private readonly Dictionary<string, PlaybackItem> _mapping;

        private long _threadId;

        private List<PlaybackItem> _items;
        private Queue<Message> _queue;

        public event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        public event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        public event TypedEventHandler<IPlaybackService, object> PositionChanged;
        public event EventHandler PlaylistChanged;

        public PlaybackService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            if (!ApiInfo.IsMediaSupported)
            {
                return;
            }

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
            _mediaPlayer.MediaFailed += OnMediaFailed;
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.SourceChanged += OnSourceChanged;
            _mediaPlayer.CommandManager.IsEnabled = false;
            _mediaPlayer.Volume = _settingsService.VolumeLevel;

            _transport = _mediaPlayer.SystemMediaTransportControls;
            _transport.ButtonPressed += Transport_ButtonPressed;

            _transport.AutoRepeatMode = _settingsService.Playback.RepeatMode;
            _isRepeatEnabled = _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.List;

            _mapping = new Dictionary<string, PlaybackItem>();
        }

        #region SystemMediaTransportControls

        private void Transport_AutoRepeatModeChangeRequested(SystemMediaTransportControls sender, AutoRepeatModeChangeRequestedEventArgs args)
        {
            IsRepeatEnabled = args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : false;
        }

        private void Transport_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    Pause();
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    _mediaPlayer.StepBackwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    _mediaPlayer.StepForwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    if (Position.TotalSeconds > 5)
                    {
                        Seek(TimeSpan.Zero);
                    }
                    else
                    {
                        MovePrevious();
                    }
                    break;
                case SystemMediaTransportControlsButton.Next:
                    MoveNext();
                    break;
            }
        }

        #endregion

        private void OnSourceChanged(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                CurrentPlayback = item;

                var message = item.Message;
                if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                {
                    message.ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
                }
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                if (item.Message.Content is MessageAudio && _isRepeatEnabled == null)
                {
                    Play();
                }
                else
                {
                    var index = _items.IndexOf(item);
                    if (index == -1 || index == (_isReversed ? 0 : _items.Count - 1))
                    {
                        if (item.Message.Content is MessageAudio && _isRepeatEnabled == true)
                        {
                            _mediaPlayer.Source = _items[_isReversed ? _items.Count - 1 : 0].Source;
                            Play();
                        }
                        else
                        {
                            Clear();
                        }
                    }
                    else
                    {
                        _mediaPlayer.Source = _items[_isReversed ? index - 1 : index + 1].Source;
                        Play();
                    }
                }
            }
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Clear();
            MediaFailed?.Invoke(this, args);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackRate)
            {
                sender.PlaybackRate = _playbackRate;
            }

            switch (sender.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlaybackState.Paused:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaPlaybackState.None:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    PlaybackState = MediaPlaybackState.None;
                    break;
            }

            //else if (sender.PlaybackState == MediaPlaybackState.Paused && sender.Position == sender.NaturalDuration && _playlist.CurrentItem == _playlist.Items.LastOrDefault())
            //{
            //    Clear();
            //}
            //else
            //{
            //    Debugger.Break();
            //}

            //PlaybackStateChanged?.Invoke(this, args);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(this, args);
        }

        private async void UpdateTransport()
        {
            var items = _items;
            var item = CurrentPlayback;

            if (items == null || item?.File == null)
            {
                _transport.IsEnabled = false;
                _transport.DisplayUpdater.ClearAll();
                return;
            }

            _transport.IsEnabled = true;
            _transport.IsPlayEnabled = true;
            _transport.IsPauseEnabled = true;
            _transport.IsPreviousEnabled = true;
            _transport.IsNextEnabled = items.Count > 1;

            void SetProperties(string title, string artist)
            {
                _transport.DisplayUpdater.ClearAll();
                _transport.DisplayUpdater.Type = MediaPlaybackType.Music;

                try
                {
                    _transport.DisplayUpdater.MusicProperties.Title = title ?? string.Empty;
                    _transport.DisplayUpdater.MusicProperties.Artist = artist ?? string.Empty;
                }
                catch { }
            }

            if (item.File.Local.IsFileExisting())
            {
                try
                {
                    var file = await item.Message.ProtoService.GetFileAsync(item.File);
                    await _transport.DisplayUpdater.CopyFromFileAsync(MediaPlaybackType.Music, file);
                }
                catch
                {
                    SetProperties(item.Title, item.Artist);
                }
            }
            else
            {
                SetProperties(item.Title, item.Artist);
            }

            _transport.DisplayUpdater.Update();
        }

        public IReadOnlyList<PlaybackItem> Items => _items ?? (IReadOnlyList<PlaybackItem>)new PlaybackItem[0];

        private PlaybackItem _currentPlayback;
        public PlaybackItem CurrentPlayback
        {
            get => _currentPlayback;
            private set
            {
                _currentItem = value?.Message;
                _currentPlayback = value;
                RaisePropertyChanged(nameof(CurrentItem));
                UpdateTransport();
            }
        }
        private MessageWithOwner _currentItem;
        public MessageWithOwner CurrentItem => _currentItem;

        public TimeSpan Position
        {
            get
            {
                try
                {
                    return _mediaPlayer?.PlaybackSession?.Position ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                try
                {
                    return _mediaPlayer?.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        private MediaPlaybackState _playbackState;
        public MediaPlaybackState PlaybackState
        {
            get { return _playbackState; }
            private set
            {
                if (_playbackState != value)
                {
                    _playbackState = value;
                    PlaybackStateChanged?.Invoke(this, null);
                }
            }
            //get
            //{
            //    try
            //    {
            //        return _mediaPlayer?.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;
            //    }
            //    catch
            //    {
            //        return MediaPlaybackState.None;
            //    }
            //}
        }

        private bool? _isRepeatEnabled = false;
        public bool? IsRepeatEnabled
        {
            get => _isRepeatEnabled;
            set
            {
                _isRepeatEnabled = value;
                _transport.AutoRepeatMode =
                    _settingsService.Playback.RepeatMode = value == true
                        ? MediaPlaybackAutoRepeatMode.List
                        : value == null
                        ? MediaPlaybackAutoRepeatMode.Track
                        : MediaPlaybackAutoRepeatMode.None;
            }
        }

        private bool _isReversed = false;
        public bool IsReversed
        {
            get => _isReversed;
            set => _isReversed = value;
        }

        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                _isShuffleEnabled = value;
                _transport.ShuffleEnabled = value;
            }
        }

        private double _playbackRate = 1.0;
        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                _playbackRate = value;
                _settingsService.Playback.PlaybackRate = value;
                try
                {
                    _mediaPlayer.PlaybackSession.PlaybackRate = value;
                    _transport.PlaybackRate = value;
                }
                catch { }
            }
        }

        public double Volume
        {
            get => _mediaPlayer.Volume;
            set
            {
                _settingsService.VolumeLevel = value;
                _mediaPlayer.Volume = value;
            }
        }

        public void Pause()
        {
            if (_mediaPlayer.PlaybackSession.CanPause)
            {
                _mediaPlayer.Pause();
                PlaybackState = MediaPlaybackState.Paused;
            }
        }

        public void Play()
        {
            _mediaPlayer.Play();
            PlaybackState = MediaPlaybackState.Playing;
        }

        public void Seek(TimeSpan span)
        {
            _mediaPlayer.PlaybackSession.Position = span;
            //var index = _playlist.CurrentItemIndex;
            //var playing = _mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

            //_mediaPlayer.Pause();
            //_playlist.MoveTo(index);
            //_mediaPlayer.PlaybackSession.Position = span;

            //if (playing)
            //{
            //    _mediaPlayer.Play();
            //}
        }

        public void MoveNext()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? 0 : items.Count - 1))
            {
                SetSource(items, _isReversed ? items.Count - 1 : 0);
            }
            else
            {
                SetSource(items, _isReversed ? index - 1 : index + 1);
            }

            Play();
        }

        public void MovePrevious()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? items.Count - 1 : 0))
            {
                SetSource(items, _isReversed ? 0 : items.Count - 1);
            }
            else
            {
                SetSource(items, _isReversed ? index + 1 : index - 1);
            }

            Play();
        }

        private void SetSource(List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                _mediaPlayer.Source = items[index].Source;
            }
        }

        public void Clear()
        {
            PlaybackState = MediaPlaybackState.None;

            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose();
        }

        public void Play(MessageWithOwner message, long threadId)
        {
            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null && _threadId == threadId)
            {
                var already = previous.FirstOrDefault(x => x.Message.Id == message.Id && x.Message.ChatId == message.ChatId);
                if (already != null)
                {
                    _mediaPlayer.Source = already.Source;
                    Play();

                    return;
                }
            }

            Dispose();

            if (message.Content is not MessageAudio)
            {
                PlaybackRate = 1;
            }
            else
            {
                PlaybackRate = _settingsService.Playback.PlaybackRate;
            }

            var item = GetPlaybackItem(message);
            var items = _items = new List<PlaybackItem>();

            _items.Add(item);
            _threadId = threadId;

            _mediaPlayer.Source = item.Source;
            Play();

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceAndVideoNote();

            message.ProtoService.Send(new SearchChatMessages(message.ChatId, string.Empty, null, message.Id, offset, 100, filter, _threadId), result =>
            {
                if (result is Messages messages)
                {
                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderBy(x => x.Id) : messages.MessagesValue.OrderByDescending(x => x.Id))
                    {
                        if (add.Id > message.Id && add.Content is MessageAudio)
                        {
                            items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                        else if (add.Id < message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                        {
                            items.Insert(0, GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                    }

                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderByDescending(x => x.Id) : messages.MessagesValue.OrderBy(x => x.Id))
                    {
                        if (add.Id < message.Id && add.Content is MessageAudio)
                        {
                            items.Add(GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                        else if (add.Id > message.Id && (add.Content is MessageVoiceNote || add.Content is MessageVideoNote))
                        {
                            items.Add(GetPlaybackItem(new MessageWithOwner(message.ProtoService, add)));
                        }
                    }

                    UpdateTransport();
                    PlaylistChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private PlaybackItem GetPlaybackItem(MessageWithOwner message)
        {
            var token = $"{message.ChatId}_{message.Id}";
            var file = message.GetFile();

            var source = CreateMediaSource(message, file);
            var item = new PlaybackItem(source)
            {
                File = file,
                Message = message,
                Token = token
            };

            if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    item.Title = audio.Audio.FileName;
                    item.Artist = string.Empty;
                }
                else
                {
                    item.Title = string.IsNullOrEmpty(audio.Audio.Title) ? Strings.Resources.AudioUnknownTitle : audio.Audio.Title;
                    item.Artist = string.IsNullOrEmpty(audio.Audio.Performer) ? Strings.Resources.AudioUnknownArtist : audio.Audio.Performer;
                }
            }

            _mapping[token] = item;

            return item;
        }

        private MediaSource CreateMediaSource(MessageWithOwner message, File file)
        {
            var token = $"{message.ChatId}_{message.Id}";

            var mime = GetMimeType(message);
            var duration = GetDuration(message);

            var stream = new RemoteFileStream(message.ProtoService, file, duration);
            var source = MediaSource.CreateFromStream(stream, mime);

            source.CustomProperties["file"] = file.Id;
            source.CustomProperties["message"] = message.Id;
            source.CustomProperties["chat"] = message.ChatId;
            source.CustomProperties["token"] = token;

            return source;
        }

        private string GetMimeType(MessageWithOwner message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.MimeType;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.MimeType;
            }
            else if (message.Content is MessageVideoNote)
            {
                return "video/mp4";
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.MimeType;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.MimeType;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return "video/mp4";
                }
            }

            return null;
        }

        private int GetDuration(MessageWithOwner message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.Duration;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Duration;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Duration;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.Duration;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Duration;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return text.WebPage.VideoNote.Duration;
                }
            }

            return 0;
        }

        private void Dispose()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.CommandManager.IsEnabled = false;
            }

            //if (_playlist != null)
            //{
            //    _playlist.CurrentItemChanged -= OnCurrentItemChanged;
            //    _playlist.Items.Clear();
            //    _playlist = null;
            //}

            if (_queue != null)
            {
                _queue.Clear();
                _queue = null;
            }

            if (_items != null)
            {
                // TODO: anything else?
                _items = null;
            }

            if (_mapping != null)
            {
                _mapping.Clear();
            }
        }

        public bool IsSupportedPlaybackRateRange(double min, double max)
        {
            if (_mediaPlayer != null)
            {
                return _mediaPlayer.PlaybackSession.IsSupportedPlaybackRateRange(min, max);
            }

            return false;
        }
    }

    public class PlaybackItem
    {
        public MediaSource Source { get; set; }
        public string Token { get; set; }

        public MessageWithOwner Message { get; set; }

        public File File { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }

        public PlaybackItem(MediaSource source)
        {
            Source = source;
        }
    }
}
