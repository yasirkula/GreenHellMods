using RuntimeUnityEditor.Core;
using System.Collections.Generic;
using UnityEngine;

/* RuntimeUnityEditor plugin is used for the runtime inspector: https://github.com/ManlyMarco/RuntimeUnityEditor/blob/master/LICENSE
 * GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007
 * Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 * 
 * Modified by yasirkula on 21 February 2021:
 * - Removed Gizmos and REPL-related classes
 * - Removed AssetBundleManagerHelper.cs
 * - Removed wireframe rendering button from Hierarchy
 * - Removed custom skin from InterfaceMaker
 * - Removed cursor functions from RuntimeUnityEditorCore
 * - Changed UnityFeatureHelper.OpenLog's path
*/
namespace GreenHell_RuntimeInspectorExt
{
	public class MainMenuExtended : MainMenu
	{
		protected override void Start()
		{
			base.Start();
			RuntimeInspector.Initialize();
		}
	}

	public class RuntimeInspector : MonoBehaviour
	{
		private sealed class Logger : ILoggerWrapper
		{
			public void Log( LogLevel logLevel, object log )
			{
				if( log != null )
					ModAPI.Log.Write( string.Concat( logLevel, ": ", log.ToString() ) );
			}
		}

		private static RuntimeInspector instance;
		private RuntimeUnityEditorCore inspector;

		private KeyCode[] toggleKey = new KeyCode[0];
		private float toggleKeyHeldTime = 0f;
		private bool toggleKeyTriggered = false;
		private bool rmbHeld = false;

		public static void Initialize()
		{
			if( !instance )
			{
				instance = new GameObject( "__RuntimeInspector__" ).AddComponent<RuntimeInspector>();
				DontDestroyOnLoad( instance.gameObject );
			}
		}

		private void Start()
		{
			inspector = new RuntimeUnityEditorCore( this, new Logger() )
			{
				EnableMouseInspect = false,
				HierarchyUpdateInterval = 0.5f
			};

			toggleKey = GetConfigurableKey( "RuntimeInspector", "ToggleKey" );
		}

		private void OnGUI()
		{
			inspector.OnGUI();
		}

		private void Update()
		{
			// Don't toggle the runtime inspector while typing something to chat
			if( toggleKey.Length > 0 && ( !InputsManager.Get() || !InputsManager.Get().m_TextInputActive ) )
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

							// Toggle inspector's visibility
							inspector.Show = !inspector.Show;
							SetCursorVisibility( inspector.Show );
						}
					}
					else
						toggleKeyTriggered = false;
				}
			}

			// Allow rotating the camera while RMB is held
			if( inspector.Show && Player.Get() )
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

			inspector.Update();
		}

		private void SetCursorVisibility( bool isVisible )
		{
			CursorManager.Get().ShowCursor( isVisible, false );

			Player player = Player.Get();
			if( player )
			{
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