using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
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

	[Serializable]
	public class SaveData
	{
		public string[] DisabledCameraComponents;
		public PostProcessLayer.Antialiasing? AntiAliasingMode;
		public string[] DisabledPostProcessingFX, DisabledPostProcessingVolumes;
		public float? DayAmbientIntensity, NightAmbientIntensity, LightSaturation;
		public AnisotropicFiltering? AnisotropicFiltering;
		public float? LODDistanceMultiplier;
		public int? LODAggressiveness, MaximumPixelLights;
		public ShadowQuality? Shadows;
		public bool? SoftParticles, SoftVegetation;
		public SkinWeights? AnimatedSkinQuality;
	}

	public class MoreGraphicsSettings : MonoBehaviour
	{
		private const int WINDOW_ID = 300553;

		private string SaveFilePath => Application.dataPath + "/../Mods/MoreGraphicsSettings.xml";

		private bool uiVisible = false;
		private Rect windowRect;

		private KeyCode[] toggleKey;
		private float toggleKeyHeldTime = 0f;
		private bool toggleKeyTriggered = false;
		private bool rmbHeld = false;

		private TOD_Sky skyComponent;
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

		private GUIStyle wordWrappingBoxStyle;

		private Vector2 scrollPos;

		private void Start()
		{
			windowRect = new Rect( ( Screen.width - 450f ) * 0.5f, Screen.height * 0.2f, 450f, Screen.height * 0.6f );
			toggleKey = GetConfigurableKey( "MoreGraphicsSettings", "ToggleKey" );
		}

		private void Update()
		{
			// Don't toggle the menu while typing something to chat
			if( toggleKey.Length > 0 && !InputsManager.Get().m_TextInputActive )
			{
				// Check if configurable key is held
				// First, make sure that all modifier keys are held
				bool modifierKeysHeld = true;
				for( int i = 0; i < toggleKey.Length - 1; i++ )
				{
					if( !Input.GetKey( toggleKey[i] ) )
					{
						modifierKeysHeld = false;
						toggleKeyTriggered = false;
						break;
					}
				}

				if( modifierKeysHeld )
				{
					if( !toggleKeyTriggered && Input.GetKeyDown( toggleKey[toggleKey.Length - 1] ) )
					{
						toggleKeyTriggered = true;
						toggleKeyHeldTime = 0f;
					}
					else if( toggleKeyTriggered && Input.GetKey( toggleKey[toggleKey.Length - 1] ) )
					{
						toggleKeyHeldTime += Time.unscaledDeltaTime;
						if( toggleKeyHeldTime >= 0.5f )
						{
							toggleKeyTriggered = false;
							ToggleUI();
						}
					}
					else
						toggleKeyTriggered = false;
				}
			}

			// Allow rotating the camera while RMB is held
			if( uiVisible )
			{
				if( !rmbHeld && Input.GetMouseButtonDown( 1 ) )
				{
					rmbHeld = true;
					Player.Get().UnblockRotation();
				}
				else if( rmbHeld && Input.GetMouseButtonUp( 1 ) )
				{
					rmbHeld = false;
					Player.Get().BlockRotation();
				}
			}

			// Allow closing the menu with ESC key
			if( uiVisible && !InputsManager.Get().m_TextInputActive && Input.GetKeyDown( KeyCode.Escape ) )
				SetUIVisible( false );
		}

		private void ToggleUI()
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

				skyComponent = TOD_Sky.Instance;
				components = cameraComponents.ToArray();
				postProcessing = PostProcessManager.Get() ? PostProcessManager.Get().GetComponentsInChildren<PostProcessVolume>() : new PostProcessVolume[0];
			}
		}

		private void OnGUI()
		{
			if( !uiVisible )
				return;

			GUI.skin = ModAPI.Interface.Skin;
			if( wordWrappingBoxStyle == null )
			{
				wordWrappingBoxStyle = new GUIStyle( GUI.skin.box )
				{
					wordWrap = true,
					stretchWidth = true
				};
			}

			windowRect = GUILayout.Window( WINDOW_ID, windowRect, WindowOnGUI, "- More Graphics Settings -" );

			// Allow closing the menu with ESC key
			if( Event.current.isKey && Event.current.keyCode == KeyCode.Escape && !InputsManager.Get().m_TextInputActive )
			{
				SetUIVisible( false );
				GUI.FocusControl( null ); // Release keyboard focus from TextFields
			}

			// While interacting with the menu or scrolling through it, don't send input to game (i.e. don't fire an arrow or switch between weapons)
			if( windowRect.Contains( Event.current.mousePosition ) && ( !Mathf.Approximately( Input.mouseScrollDelta.y, 0f ) || Input.GetMouseButton( 0 ) ) )
				Input.ResetInputAxes();
		}

		private void WindowOnGUI( int id )
		{
			numberFieldIndex = 0;

			GUILayout.BeginVertical( GUI.skin.box );
			GUILayout.BeginHorizontal( GUILayout.Height( 40f ) );

			// Show save & load buttons at the top left corner
			if( GUILayout.Button( "SAVE", GUILayout.Width( 100f ), GUILayout.Height( 40f ) ) )
			{
				SaveSettings();
				GUI.FocusControl( null ); // Release keyboard focus from TextFields
			}

			if( GUILayout.Button( "LOAD", GUILayout.Width( 100f ), GUILayout.Height( 40f ) ) )
			{
				LoadSettings();
				GUI.FocusControl( null ); // Release keyboard focus from TextFields
			}

			GUILayout.FlexibleSpace();

			// Show a close button at the top right corner
			if( GUILayout.Button( "X", GUILayout.Width( 40f ), GUILayout.Height( 40f ) ) )
			{
				SetUIVisible( false );
				GUI.FocusControl( null ); // Release keyboard focus from TextFields
			}

			GUILayout.EndHorizontal();
			GUILayout.Space( 5f );

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
					GUILayout.Box( "Camera's PostProcessLayer component must be enabled to modify these values.", wordWrappingBoxStyle );
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
					GUILayout.Box( "Game post processing volume must be enabled to modify these values.", wordWrappingBoxStyle );
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

			// Allow changing light intensity
			if( skyComponent )
			{
				GUILayout.Space( 15f );
				GUILayout.Label( "= LIGHTING SETTINGS =" );

				skyComponent.Day.AmbientMultiplier = FloatField( "Day Ambient Intensity: ", skyComponent.Day.AmbientMultiplier );
				skyComponent.Night.AmbientMultiplier = FloatField( "Night Ambient Intensity: ", skyComponent.Night.AmbientMultiplier );
				skyComponent.Ambient.Saturation = FloatField( "Light Saturation: ", skyComponent.Ambient.Saturation );
			}

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
			GUILayout.EndVertical();

			GUI.DragWindow();
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
			// value (like only allowing values 1, 2 or 4) messes up with UX: if "1" is entered to TextField and user attempts
			// to clear it via Backspace and then type "2" or "4", it won't work because when "1" is cleared, resulting ""
			// isn't a valid input (even though user was planning to change it to "2" or "4" shortly) and the TextField would be
			// reset back to "1" immediately
			bool _guiEnabled = GUI.enabled;
			GUI.enabled = numberFieldStrValues[numberFieldIndex] != value;
			if( GUILayout.Button( "Apply" ) )
			{
				onValueChanged( numberFieldStrValues[numberFieldIndex] );

				// We aren't just saying "numberFieldStrValues[numberFieldIndex] = value" here because value can further
				// be modified by the caller function in OnGUI. We want to fetch that updated value in the next frame
				numberFieldUpdateStrValues[numberFieldIndex] = true;

				// Release keyboard focus from TextField
				GUI.FocusControl( null );
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
				if( rmbHeld )
					rmbHeld = false;
				else
					player.UnblockRotation();

				player.UnblockInspection();
			}
		}

		private void SaveSettings()
		{
			// Must be invoked from OnGUI
			if( !uiVisible )
				return;

			SaveData saveData = new SaveData();

			List<string> disabledCameraComponents = new List<string>( components.Length );
			for( int i = 0; i < components.Length; i++ )
			{
				Behaviour component = components[i] as Behaviour;
				if( component && !component.enabled )
					disabledCameraComponents.Add( component.GetType().FullName );
			}

			saveData.DisabledCameraComponents = disabledCameraComponents.ToArray();

			PostProcessLayer ppLayer = CameraManager.Get().m_MainCamera.GetComponent<PostProcessLayer>();
			if( ppLayer )
				saveData.AntiAliasingMode = ppLayer.antialiasingMode;

			PostProcessVolume mainPP = PostProcessManager.Get().GetVolume( PostProcessManager.Effect.Game );
			if( mainPP && mainPP.profile )
			{
				List<PostProcessEffectSettings> mainPP_FX = mainPP.profile.settings;
				List<string> disabledPostProcessingFX = new List<string>( mainPP_FX.Count );
				for( int i = 0; i < mainPP_FX.Count; i++ )
				{
					if( !mainPP_FX[i].enabled )
						disabledPostProcessingFX.Add( mainPP_FX[i].name );
				}

				saveData.DisabledPostProcessingFX = disabledPostProcessingFX.ToArray();
			}

			List<string> disabledPostProcessingVolumes = new List<string>( postProcessing.Length );
			for( int i = 0; i < postProcessing.Length; i++ )
			{
				PostProcessVolume pp = postProcessing[i];
				if( pp && !pp.enabled )
					disabledPostProcessingVolumes.Add( pp.name );
			}

			saveData.DisabledPostProcessingVolumes = disabledPostProcessingVolumes.ToArray();

			if( skyComponent )
			{
				saveData.DayAmbientIntensity = skyComponent.Day.AmbientMultiplier;
				saveData.NightAmbientIntensity = skyComponent.Night.AmbientMultiplier;
				saveData.LightSaturation = skyComponent.Ambient.Saturation;
			}

			saveData.AnisotropicFiltering = QualitySettings.anisotropicFiltering;
			saveData.LODDistanceMultiplier = QualitySettings.lodBias;
			saveData.LODAggressiveness = QualitySettings.maximumLODLevel;
			saveData.MaximumPixelLights = QualitySettings.pixelLightCount;
			saveData.Shadows = QualitySettings.shadows;
			saveData.SoftParticles = QualitySettings.softParticles;
			saveData.SoftVegetation = QualitySettings.softVegetation;
			saveData.AnimatedSkinQuality = QualitySettings.skinWeights;

			string saveFilePath = SaveFilePath;
			Directory.CreateDirectory( Path.GetDirectoryName( saveFilePath ) );
			using( TextWriter stream = new StreamWriter( saveFilePath ) )
			{
				new XmlSerializer( typeof( SaveData ) ).Serialize( stream, saveData );
			}
		}

		private void LoadSettings()
		{
			// Must be invoked from OnGUI
			if( !uiVisible )
				return;

			string saveFilePath = SaveFilePath;
			if( !File.Exists( saveFilePath ) )
				return;

			SaveData saveData = null;
			using( FileStream stream = new FileStream( saveFilePath, FileMode.Open, FileAccess.Read ) )
			{
				saveData = (SaveData) new XmlSerializer( typeof( SaveData ) ).Deserialize( stream );
			}

			if( saveData == null )
				return;

			numberFieldStrValues.Clear();
			numberFieldUpdateStrValues.Clear();

			if( saveData.DisabledCameraComponents != null )
			{
				for( int i = 0; i < components.Length; i++ )
				{
					Behaviour component = components[i] as Behaviour;
					if( component && Array.IndexOf( saveData.DisabledCameraComponents, component.GetType().FullName ) >= 0 )
						component.enabled = false;
				}
			}

			if( saveData.AntiAliasingMode.HasValue )
			{
				PostProcessLayer ppLayer = CameraManager.Get().m_MainCamera.GetComponent<PostProcessLayer>();
				if( ppLayer )
					ppLayer.antialiasingMode = saveData.AntiAliasingMode.Value;
			}

			if( saveData.DisabledPostProcessingFX != null )
			{
				PostProcessVolume mainPP = PostProcessManager.Get().GetVolume( PostProcessManager.Effect.Game );
				if( mainPP && mainPP.profile )
				{
					List<PostProcessEffectSettings> mainPP_FX = mainPP.profile.settings;
					for( int i = 0; i < mainPP_FX.Count; i++ )
					{
						if( Array.IndexOf( saveData.DisabledPostProcessingFX, mainPP_FX[i].name ) >= 0 )
							mainPP_FX[i].enabled.value = false;
					}
				}
			}

			if( saveData.DisabledPostProcessingVolumes != null )
			{
				for( int i = 0; i < postProcessing.Length; i++ )
				{
					PostProcessVolume pp = postProcessing[i];
					if( pp && Array.IndexOf( saveData.DisabledPostProcessingVolumes, pp.name ) >= 0 )
						pp.enabled = false;
				}
			}

			if( skyComponent )
			{
				if( saveData.DayAmbientIntensity.HasValue )
					skyComponent.Day.AmbientMultiplier = saveData.DayAmbientIntensity.Value;
				if( saveData.NightAmbientIntensity.HasValue )
					skyComponent.Night.AmbientMultiplier = saveData.NightAmbientIntensity.Value;
				if( saveData.LightSaturation.HasValue )
					skyComponent.Ambient.Saturation = saveData.LightSaturation.Value;
			}

			if( saveData.AnisotropicFiltering.HasValue )
				QualitySettings.anisotropicFiltering = saveData.AnisotropicFiltering.Value;
			if( saveData.LODDistanceMultiplier.HasValue )
				QualitySettings.lodBias = saveData.LODDistanceMultiplier.Value;
			if( saveData.LODAggressiveness.HasValue )
				QualitySettings.maximumLODLevel = saveData.LODAggressiveness.Value;
			if( saveData.MaximumPixelLights.HasValue )
				QualitySettings.pixelLightCount = saveData.MaximumPixelLights.Value;
			if( saveData.Shadows.HasValue )
				QualitySettings.shadows = saveData.Shadows.Value;
			if( saveData.SoftParticles.HasValue )
				QualitySettings.softParticles = saveData.SoftParticles.Value;
			if( saveData.SoftVegetation.HasValue )
				QualitySettings.softVegetation = saveData.SoftVegetation.Value;
			if( saveData.AnimatedSkinQuality.HasValue )
				QualitySettings.skinWeights = saveData.AnimatedSkinQuality.Value;

			GUIUtility.ExitGUI();
		}

		// Returns configurable key's corresponding KeyCode(s) by parsing RuntimeConfiguration.xml
		private KeyCode[] GetConfigurableKey( string modID, string keyID )
		{
			List<KeyCode> keys = new List<KeyCode>( 2 );

			string configurationFile = Application.dataPath + "/../Mods/RuntimeConfiguration.xml";
			if( System.IO.File.Exists( configurationFile ) )
			{
				string configuration = System.IO.File.ReadAllText( configurationFile );
				string modTag = "<Mod ID=\"" + modID + "\"";
				int modTagStart = configuration.IndexOf( modTag );
				if( modTagStart >= 0 )
				{
					int nextModTagStart = configuration.IndexOf( "<Mod ID=", modTagStart + modTag.Length );
					int modTagEnd = ( nextModTagStart > modTagStart ) ? nextModTagStart : configuration.Length;

					string keyTag = "<Button ID=\"" + keyID + "\">";
					int keyTagStart = configuration.IndexOf( keyTag, modTagStart + modTag.Length );
					if( keyTagStart > modTagStart && keyTagStart < modTagEnd )
					{
						int keyTagEnd = configuration.IndexOf( "</Button>", keyTagStart + keyTag.Length );
						if( keyTagEnd > keyTagStart && keyTagEnd < modTagEnd )
						{
							string[] keyRawSplit = configuration.Substring( keyTagStart + keyTag.Length, keyTagEnd - keyTagStart - keyTag.Length ).Split( '+' );
							for( int i = 0; i < keyRawSplit.Length; i++ )
							{
								// Fix typos in common modifier keys
								if( keyRawSplit[i] == "LeftCtrl" )
									keyRawSplit[i] = "LeftControl";
								else if( keyRawSplit[i] == "RightCtrl" )
									keyRawSplit[i] = "RightControl";

								if( keyRawSplit[i].Length > 0 && System.Enum.IsDefined( typeof( KeyCode ), keyRawSplit[i] ) )
									keys.Add( (KeyCode) System.Enum.Parse( typeof( KeyCode ), keyRawSplit[i] ) );
							}
						}
					}
				}
			}

			return keys.ToArray();
		}
	}
}