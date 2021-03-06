﻿using MediaBrowser.Common.Events;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.Theming;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Theater.Implementations.Session
{
    public class SessionManager : ISessionManager
    {
        public event EventHandler<EventArgs> UserLoggedIn;

        public event EventHandler<EventArgs> UserLoggedOut;

        private readonly IApiClient _apiClient;
        private readonly INavigationService _navService;
        private readonly ILogger _logger;
        private readonly IThemeManager _themeManager;
        private readonly ITheaterConfigurationManager _config;
        private readonly IPlaybackManager _playback;

        public SessionManager(INavigationService navService, IApiClient apiClient, ILogger logger, IThemeManager themeManager, ITheaterConfigurationManager config, IPlaybackManager playback)
        {
            _navService = navService;
            _apiClient = apiClient;
            _logger = logger;
            _themeManager = themeManager;
            _config = config;
            _playback = playback;
        }

        public UserDto CurrentUser { get; private set; }

        public async Task Logout()
        {
             _playback.StopAllPlayback();

            _apiClient.CurrentUserId = null;

            var previous = CurrentUser;

            CurrentUser = null;

            if (previous != null)
            {
                EventHelper.FireEventIfNotNull(UserLoggedOut, this, EventArgs.Empty, _logger);
            }

            await _navService.NavigateToLoginPage();

            _navService.ClearHistory();
        }

        public async Task Login(string username, string password)
        {
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));

                var result = await _apiClient.AuthenticateUserAsync(username, hash);

                CurrentUser = result.User;
                _apiClient.CurrentUserId = CurrentUser.Id;
            }

            EventHelper.FireEventIfNotNull(UserLoggedIn, this, EventArgs.Empty, _logger);

            var userConfig = await _config.GetUserTheaterConfiguration(CurrentUser.Id);

            var theme = _themeManager.Themes.FirstOrDefault(i => string.Equals(i.Name, userConfig.Theme)) ?? _themeManager.DefaultTheme;

            await _themeManager.LoadTheme(theme);

            await _navService.NavigateToHomePage();

            _navService.ClearHistory();
        }
    }
}
