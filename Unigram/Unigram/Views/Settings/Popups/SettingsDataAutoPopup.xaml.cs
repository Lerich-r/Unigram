﻿using System;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings.Popups
{
    public sealed partial class SettingsDataAutoPopup : ContentPopup
    {
        public SettingsDataAutoViewModel ViewModel => DataContext as SettingsDataAutoViewModel;

        public SettingsDataAutoPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Resources.Done;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        #region Binding

        private const long MAX_FILE_SIZE = 1024L * 1024L * 2000L;

        private double ConvertLimit(long size)
        {
            var progress = 0.0d;
            size -= 500 * 1024;
            if (size < 524 * 1024)
            {
                progress = Math.Max(0, size / (double)(524 * 1024)) * 0.25f;
            }
            else
            {
                progress += 0.25f;
                size -= 524 * 1024;

                if (size < 1024 * 1024 * 9)
                {
                    progress += Math.Max(0, size / (double)(9 * 1024 * 1024)) * 0.25f;
                }
                else
                {
                    progress += 0.25f;
                    size -= 9 * 1024 * 1024;

                    if (size < 1024 * 1024 * 90)
                    {
                        progress += Math.Max(0, size / (double)(90 * 1024 * 1024)) * 0.25f;
                    }
                    else
                    {
                        progress += 0.25f;
                        size -= 90 * 1024 * 1024;

                        progress += Math.Max(0, size / (double)(MAX_FILE_SIZE - 100 * 1024 * 1024)) * 0.25f;
                    }
                }
            }

            return progress;
        }

        private void ConvertLimitBack(double progress)
        {
            int size = 500 * 1024;
            if (progress <= 0.25f)
            {
                size += (int)(524 * 1024 * (progress / 0.25f));
            }
            else
            {
                progress -= 0.25f;
                size += 524 * 1024;

                if (progress < 0.25f)
                {
                    size += (int)(9 * 1024 * 1024 * (progress / 0.25f));
                }
                else
                {
                    progress -= 0.25f;
                    size += 9 * 1024 * 1024;

                    if (progress <= 0.25f)
                    {
                        size += (int)(90 * 1024 * 1024 * (progress / 0.25f));
                    }
                    else
                    {
                        progress -= 0.25f;
                        size += 90 * 1024 * 1024;

                        size += (int)((MAX_FILE_SIZE - size) * (progress / 0.25f));
                    }
                }
            }

            ViewModel.Limit = size;
        }

        private string ConvertUpTo(long limit)
        {
            return FileSizeConverter.Convert(limit, true);
        }

        #endregion

    }
}
