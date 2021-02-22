using Enums;
using UnityEngine;

namespace GreenHell_QuickFade
{
	public class FadeSystemExtended : FadeSystem
	{
		public override void FadeIn( FadeType type, VDelegate callback = null, float duration = 1.5f )
		{
			duration = Mathf.Min( duration, 0.1f );
			base.FadeIn( type, callback, duration );
		}

		public override void FadeOut( FadeType type, VDelegate callback = null, float duration = 1.5f, GameObject screen_prefab = null )
		{
			duration = Mathf.Min( duration, 0.1f );
			base.FadeOut( type, callback, duration, screen_prefab );
		}
	}

	public class MainMenuManagerExtended : MainMenuManager
	{
		public override void CallLoadGameFadeSequence()
		{
			OnPreLoadGame(); // <- Changed from Invoke to direct method call
		}

		protected override void OnLoadGame()
		{
			LoadingScreen.Get().Show( LoadingScreenState.StartGame );
			GreenHellGame.Instance.m_FromSave = true;

			if( Music.Get().m_Source[0] )
				Music.Get().m_Source[0].Stop(); // <- Changed from fade out to direct stop

			HideAllScreens();
			GreenHellGame.GetFadeSystem().FadeIn( FadeType.All, null, 2f );

			OnStartGameDelayed(); // <- Changed from Invoke to direct method call
		}
	}

	public class HUDSleepingExtended : HUDSleeping
	{
		protected override void OnShow()
		{
			base.OnShow();
			SetBGAlpha( 0.975f );
			m_CanvasGroup.alpha = 0.975f; // This needs to be smaller than 1 because code that switches from FadeIn to Progress won't execute otherwise
		}
	}

	public class HUDDeathExtended : HUDDeath
	{
		protected override void OnShow()
		{
			base.OnShow();

			Color color = m_BG.color;
			color.a = 0.975f;
			m_BG.color = color;
			m_CanvasGroup.alpha = 0.975f; // This needs to be smaller than 1 because code that switches from FadeIn to Progress won't execute otherwise
		}
	}
}