using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing; // Must be added to References manually: "C:\Program Files (x86)\Steam\steamapps\common\Green Hell\GH_Data\Managed\Unity.Postprocessing.Runtime.dll"

namespace GreenHell_MoreGraphicsSettings
{
	public class AddMyGameObject : Player
	{
		// Create an instance of MoreGraphicsSettings
		protected override void Start()
		{
			base.Start();
			new GameObject( "__MoreGraphicsSettings__" ).AddComponent<MoreGraphicsSettings>();
		}
	}

	public class MoreGraphicsSettings : MonoBehaviour
	{
		private bool uiVisible = false;

		private Component[] components = new Component[0];
		private PostProcessVolume[] postProcessing = new PostProcessVolume[0];

		private readonly List<string> numberFieldStrValues = new List<string>( 16 );
		private readonly List<bool> numberFieldUpdateStrValues = new List<bool>( 16 );
		private int numberFieldIndex;

		private readonly NumberFormatInfo numberParser = NumberFormatInfo.GetInstance( CultureInfo.InvariantCulture );
		private readonly string[] antiAliasingModes = new string[4] { "Off", "FXAA", "SMAA", "TAA" };
		private readonly HashSet<Type> excludedComponents = new HashSet<Type>()
		{
			typeof( Transform ), typeof( RectTransform ), typeof( Camera ), typeof( AudioListener ), typeof( AudioSource ),
			typeof( ParametersInterpolator ), typeof( Cinemachine.CinemachineBrain ), typeof( LuxWater.LuxWater_WaterVolumeTrigger )
		};

		private Vector2 scrollPos;

		private void Update()
		{
			// Toggle the menu with Pause key
			if( Input.GetKeyDown( KeyCode.Pause ) )
			{
				SetUIVisible( !uiVisible );

				if( uiVisible )
				{
					// Fetch components attached to MainCamera
					List<Component> cameraComponents = new List<Component>( 32 );
					CameraManager.Get().m_MainCamera.GetComponents<Component>( cameraComponents );
					for( int i = cameraComponents.Count - 1; i >= 0; i-- )
					{
						Component component = cameraComponents[i];
						if( !component || excludedComponents.Contains( component.GetType() ) || !( component is Behaviour ) )
							cameraComponents.RemoveAt( i );

						//ModAPI.Log.Write( "COMPONENT: " + component );
					}

					components = cameraComponents.ToArray();
					postProcessing = PostProcessManager.Get() ? PostProcessManager.Get().GetComponentsInChildren<PostProcessVolume>() : new PostProcessVolume[0];
				}
			}

			// Allow closing the menu with ESC key
			if( uiVisible && Input.GetKeyDown( KeyCode.Escape ) )
				SetUIVisible( false );
		}

		private void OnGUI()
		{
			if( !uiVisible )
				return;

			numberFieldIndex = 0;

			GUI.skin = ModAPI.Interface.Skin;

			// Show a close button at top right corner
			if( GUI.Button( new Rect( Screen.width * 0.72f, Screen.height * 0.2f - Screen.width * 0.03f, Screen.width * 0.03f, Screen.width * 0.03f ), "X" ) )
				SetUIVisible( false );

			GUILayout.BeginArea( new Rect( Screen.width * 0.25f, Screen.height * 0.2f, Screen.width * 0.5f, Screen.height * 0.6f ), GUI.skin.window );

			scrollPos = GUILayout.BeginScrollView( scrollPos );

			// Allow toggling components attached to MainCamera
			GUILayout.Space( 15f );
			GUILayout.Label( "= CAMERA COMPONENTS =" );

			for( int i = 0; i < components.Length; i++ )
			{
				Behaviour component = components[i] as Behaviour;
				if( !component )
					continue;

				bool wasEnabled = component.enabled;
				bool isEnabled = GUILayout.Toggle( wasEnabled, component.GetType().FullName );
				if( isEnabled != wasEnabled )
					component.enabled = isEnabled;
			}

			// Allow toggling main post processing volume's effects
			GUILayout.Space( 15f );
			GUILayout.Label( "= POST PROCESSING =" );

			bool _guiEnabled = GUI.enabled;
			PostProcessLayer ppLayer = CameraManager.Get().m_MainCamera.GetComponent<PostProcessLayer>();
			if( ppLayer )
			{
				if( !ppLayer.enabled )
				{
					GUILayout.Box( "Camera's PostProcessLayer component must be enabled to modify these values.", GUILayout.ExpandWidth( true ) );
					GUI.enabled = false;
				}

				GUILayout.BeginHorizontal();
				GUILayout.Label( "Anti Aliasing: ", GUILayout.ExpandWidth( false ) );
				ppLayer.antialiasingMode = (PostProcessLayer.Antialiasing) GUILayout.Toolbar( (int) ppLayer.antialiasingMode, antiAliasingModes );
				GUILayout.EndHorizontal();
			}

			PostProcessVolume mainPP = PostProcessManager.Get().GetVolume( PostProcessManager.Effect.Game );
			if( mainPP && mainPP.profile )
			{
				bool _guiEnabled2 = GUI.enabled;
				if( !mainPP.enabled )
				{
					GUI.enabled = true;
					GUILayout.Box( "Game post processing volume must be enabled to modify these values.", GUILayout.ExpandWidth( true ) );
					GUI.enabled = false;
				}

				List<PostProcessEffectSettings> mainPP_FX = mainPP.profile.settings;
				for( int i = 0; i < mainPP_FX.Count; i++ )
					mainPP_FX[i].enabled.value = GUILayout.Toggle( mainPP_FX[i].enabled.value, mainPP_FX[i].name.Replace( "(Clone)", "" ) );

				GUI.enabled = _guiEnabled2;
			}

			// Allow toggling post processing volumes (has no effect if PostProcessLayer is disabled in CAMERA COMPONENTS)
			if( postProcessing.Length > 0 )
			{
				GUILayout.Space( 15f );
				GUILayout.Label( "= POST PROCESSING VOLUMES =" );

				for( int i = 0; i < postProcessing.Length; i++ )
				{
					PostProcessVolume pp = postProcessing[i];
					if( !pp )
						continue;

					bool wasEnabled = pp.enabled;
					bool isEnabled = GUILayout.Toggle( wasEnabled, pp.name.Replace( "PostProcess_", "" ) );
					if( isEnabled != wasEnabled )
						pp.enabled = isEnabled;
				}
			}

			GUI.enabled = _guiEnabled;

			// Allow changing QualitySettings parameters
			GUILayout.Space( 15f );
			GUILayout.Label( "= QUALITY SETTINGS =" );

			bool aniso = GUILayout.Toggle( QualitySettings.anisotropicFiltering != AnisotropicFiltering.Disable, "Anisotrophic Filtering" );
			if( !aniso )
				QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
			else if( QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable )
				QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

			QualitySettings.lodBias = Mathf.Max( 0.01f, FloatField( "LOD Distance Multiplier: ", QualitySettings.lodBias ) );
			QualitySettings.maximumLODLevel = IntField( "LOD Aggressiveness: ", QualitySettings.maximumLODLevel );
			QualitySettings.pixelLightCount = Mathf.Max( 0, IntField( "Maximum Pixel Lights: ", QualitySettings.pixelLightCount ) );

			bool shadows = GUILayout.Toggle( QualitySettings.shadows != ShadowQuality.Disable, "Shadows" );
			if( !shadows )
				QualitySettings.shadows = ShadowQuality.Disable;
			else if( QualitySettings.shadows == ShadowQuality.Disable )
				QualitySettings.shadows = ShadowQuality.All;

			QualitySettings.softParticles = GUILayout.Toggle( QualitySettings.softParticles, "Soft Particles" );
			QualitySettings.softVegetation = GUILayout.Toggle( QualitySettings.softVegetation, "Soft Vegetation" );

			int skinWeights = Mathf.Max( 1, IntField( "Animated Skin Quality: ", (int) QualitySettings.skinWeights ) );
			if( skinWeights == 1 )
				QualitySettings.skinWeights = SkinWeights.OneBone;
			else if( skinWeights == 2 )
				QualitySettings.skinWeights = SkinWeights.TwoBones;
			else if( skinWeights <= 4 )
				QualitySettings.skinWeights = SkinWeights.FourBones;
			else
				QualitySettings.skinWeights = SkinWeights.Unlimited;

			GUILayout.EndScrollView();
			GUILayout.EndArea();
		}

		private int IntField( string label, int value )
		{
			NumberField( label, value.ToString(), ( newValueStr ) =>
			{
				int newValue;
				if( int.TryParse( numberFieldStrValues[numberFieldIndex], NumberStyles.Integer, numberParser, out newValue ) )
					value = newValue;
			} );

			return value;
		}

		private float FloatField( string label, float value )
		{
			NumberField( label, value.ToString(), ( newValueStr ) =>
			{
				float newValue;
				if( float.TryParse( newValueStr, NumberStyles.Float, numberParser, out newValue ) )
					value = newValue;
			} );

			return value;
		}

		private void NumberField( string label, string value, Action<string> onValueChanged )
		{
			if( numberFieldIndex >= numberFieldStrValues.Count )
			{
				numberFieldStrValues.Add( value );
				numberFieldUpdateStrValues.Add( false );
			}

			if( numberFieldUpdateStrValues[numberFieldIndex] )
			{
				numberFieldUpdateStrValues[numberFieldIndex] = false;
				numberFieldStrValues[numberFieldIndex] = value;
			}

			GUILayout.BeginHorizontal();
			GUILayout.Label( label, GUILayout.ExpandWidth( false ) );

			numberFieldStrValues[numberFieldIndex] = GUILayout.TextField( numberFieldStrValues[numberFieldIndex] );

			// Changes won't be applied until Apply button is clicked. Otherwise, trying to put restrictions to
			// value (like only allowing value 1, 2 or 4) messes up with UX: if "1" is entered to TextField and user attempts
			// to clear it via Backspace and then type "2" or "4", it won't work because when "1" is cleared, resulting ""
			// isn't a valid input (even though user was planning to change it to "2" or "4" shortly) and the TextField would be
			// reset to "1" immediately
			bool _guiEnabled = GUI.enabled;
			GUI.enabled = numberFieldStrValues[numberFieldIndex] != value;
			if( GUILayout.Button( "Apply" ) )
			{
				onValueChanged( numberFieldStrValues[numberFieldIndex] );

				// We aren't just saying "numberFieldStrValues[numberFieldIndex] = value" here because value can further
				// be modified by the caller function in OnGUI. We want to fetch that updated value in the next frame
				numberFieldUpdateStrValues[numberFieldIndex] = true;
			}
			GUI.enabled = _guiEnabled;

			GUILayout.EndHorizontal();

			numberFieldIndex++;
		}

		private void SetUIVisible( bool isVisible )
		{
			uiVisible = isVisible;

			CursorManager.Get().ShowCursor( isVisible, false );
			Player player = Player.Get();

			if( isVisible )
			{
				player.BlockRotation();
				player.BlockInspection();
			}
			else
			{
				player.UnblockRotation();
				player.UnblockInspection();
			}
		}
	}
}