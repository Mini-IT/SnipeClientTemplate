using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using MiniIT;

#if UNITY_ANDROID
using GooglePlayGames;
#endif

namespace MiniIT.Social
{
	public class GooglePlayProvider : SocialProvider
	{
		public static event Action InstanceInitializationComplete;

		public const string DUMMY_AVATAR_URL = ""; //"https://cdn.tuner-life.com/tl2/dummyavatar50.png";

		private static GooglePlayProvider sInstance;
		public static GooglePlayProvider Instance
		{
			get
			{
				if (sInstance == null)
					new GooglePlayProvider();
				return sInstance;
			}
		}

		public static bool InstanceInitialized
		{
			get
			{
				return sInstance != null && sInstance.Initialized;
			}
		}

		public bool AuthenticationFailed
		{
			get
			{
				return PlayerPrefs.GetInt("GooglePlayProvider_AuthenticationFailed", 0) == 1;
			}
			set
			{
				PlayerPrefs.SetInt("GooglePlayProvider_AuthenticationFailed", value ? 1 : 0);
			}
		}

		private List<Action> mOnInitializationSucceededCallbacks = null;
		private List<Action> mOnInitializationFailedCallbacks = null;

		//private bool mInitialServerAuthCodeUsed;

		public GooglePlayProvider () : base(SocialNetworkType.GOOGLE_PLAY)
		{
#if UNITY_ANDROID
			//mInitialServerAuthCodeUsed = false;

			GooglePlayGames.BasicApi.PlayGamesClientConfiguration config = new GooglePlayGames.BasicApi.PlayGamesClientConfiguration.Builder()
				//.RequestServerAuthCode(false)  // needed only for GetServerAuthCode()
				.AddOauthScope("openid")
				.RequestIdToken()
				.Build();
			GooglePlayGames.PlayGamesPlatform.InitializeInstance(config);
			GooglePlayGames.PlayGamesPlatform.DebugLogEnabled = true;
			GooglePlayGames.PlayGamesPlatform.Activate();
#endif
			sInstance = this;
		}
		
		public override void Init(Action callback = null, Action fail_callback = null)
		{
			if (AuthenticationFailed)
			{
				if (fail_callback != null)
					fail_callback.Invoke();
				return;
			}

			if (callback != null)
			{
				if (mOnInitializationSucceededCallbacks == null)
					mOnInitializationSucceededCallbacks = new List<Action>();

				mOnInitializationSucceededCallbacks.Add(callback);
			}

			if (fail_callback != null)
			{
				if (mOnInitializationFailedCallbacks == null)
					mOnInitializationFailedCallbacks = new List<Action>();

				mOnInitializationFailedCallbacks.Add(fail_callback);
			}

			if (UnityEngine.Social.localUser.authenticated)
				OnAuthenticated();
			else
				UnityEngine.Social.localUser.Authenticate(ProcessAuthentication);
		}
		
#if UNITY_ANDROID
		public override void Logout()
		{
			if (Initialized)
			{
				((GooglePlayGames.PlayGamesPlatform)UnityEngine.Social.Active).SignOut();
				Initialized = false;
			}
			base.Logout();
		}
#endif

		// This function gets called when Authenticate completes
		// Note that if the operation is successful, Social.localUser will contain data from the server. 
		private void ProcessAuthentication(bool success)
		{
			if (success || UnityEngine.Social.localUser.authenticated)
			{
				OnAuthenticated();
			}
			else
			{
				AuthenticationFailed = true;

				if (mOnInitializationFailedCallbacks != null)
				{
					for (int i = 0; i < mOnInitializationFailedCallbacks.Count; i++)
					{
						Action callback = mOnInitializationFailedCallbacks[i];
						if (callback != null)
							callback.Invoke();
					}
					mOnInitializationFailedCallbacks.Clear();
					mOnInitializationFailedCallbacks = null;
				}

				if (mOnInitializationSucceededCallbacks != null)
				{
					mOnInitializationSucceededCallbacks.Clear();
					mOnInitializationSucceededCallbacks = null;
				}

				// Dispatch event
				DispatchEventInitializationFailed();

				InstanceInitializationComplete?.Invoke();
			}
		}

		private void OnAuthenticated()
		{
			//mViewerID = UnityEngine.Social.localUser.id;

			OnLocalUserInitialized();
		}
		
		public void GetServerAuthToken(Action<string> callback)
		{
			//if (!Initialized)
			//	return "";
#if UNITY_ANDROID
			//if (!mInitialServerAuthCodeUsed)
			//{
			//	mInitialServerAuthCodeUsed = true;
			//	callback.Invoke(PlayGamesPlatform.Instance.GetServerAuthCode());
			//}
			//else
			{
				PlayGamesPlatform.Instance.GetAnotherServerAuthCode(false, callback);
			}
			
#else
			//return "";
#endif
		}

		private void OnLocalUserInitialized(IUserProfile profile = null)
		{
			PlayerProfile = PrepareProfile(UnityEngine.Social.localUser);
			
			Initialized = true;

			if (mOnInitializationSucceededCallbacks != null)
			{
				for (int i = 0; i < mOnInitializationSucceededCallbacks.Count; i++)
				{
					Action callback = mOnInitializationSucceededCallbacks[i];
					if (callback != null)
						callback.Invoke();
				}
				mOnInitializationSucceededCallbacks.Clear();
				mOnInitializationSucceededCallbacks = null;
			}

			if (mOnInitializationFailedCallbacks != null)
			{
				mOnInitializationFailedCallbacks.Clear();
				mOnInitializationFailedCallbacks = null;
			}

			// Dispatch event
			DispatchEventInitializationComplete();
		}

		private SocialUserProfile PrepareProfile(IUserProfile data)
		{
			SocialUserProfile profile = new SocialUserProfile(data.id, SocialNetworkType.GOOGLE_PLAY);
			profile.Name           = data.userName;
			profile.PhotoSmallURL  = DUMMY_AVATAR_URL;
			profile.PhotoMediumURL = DUMMY_AVATAR_URL;

#if UNITY_ANDROID
			if (data.id == GooglePlayGames.PlayGamesPlatform.Instance.localUser.id)
			{
				string url = GooglePlayGames.PlayGamesPlatform.Instance.GetUserImageUrl();
				if (!string.IsNullOrEmpty(url))
				{
					profile.PhotoSmallURL = url;
					profile.PhotoMediumURL = url;
				}
			}
#endif
			// добавим в кэш
			//SetProfileData(profile);

			return profile;
		}

#region Leaderboard

		public void ShowLeaderboardUI(string leaderboard_id = "")
		{
#if UNITY_ANDROID
			if (!string.IsNullOrEmpty(leaderboard_id))
				(UnityEngine.Social.Active as GooglePlayGames.PlayGamesPlatform).SetDefaultLeaderboardForUI(leaderboard_id);
#endif

			UnityEngine.Social.ShowLeaderboardUI();
		}

		public void ReportScore(string board_id, long score)
		{
			if (!Initialized)
				return;
			
			UnityEngine.Social.ReportScore(score, board_id,
				(bool success) => { Debug.Log("[UnitySocialAPIProvider] ReportScore " + (success ? "success" : "fail")); }
			);
		}

#endregion  // Leaderboard

#region Achievements

		public void ShowAchievementsUI()
		{
			UnityEngine.Social.ShowAchievementsUI();
		}

		public void ReportAchievementReached(string achievement_id)
		{
			if (!Initialized)
				return;
			
			UnityEngine.Social.ReportProgress(achievement_id, 100,  // 100%
				(bool success) => { Debug.Log("[UnitySocialAPIProvider] ReportAchievementReached " + (success ? "success" : "fail")); }
			);
		}

#endregion  // Achievements
	}
}

