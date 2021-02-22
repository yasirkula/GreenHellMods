using System.Collections.Generic;
using UnityEngine;

namespace GreenHell_SaveGameFixer
{
	public class AddMyGameObject : Player
	{
		// Create an instance of SaveGameFixer
		protected override void Start()
		{
			base.Start();
			new GameObject( "__SaveGameFixer__" ).AddComponent<SaveGameFixer>();
		}
	}

	public class SaveGameFixer : MonoBehaviour
	{
		private KeyCode[] hotkey;

		private void Start()
		{
			hotkey = GetConfigurableKey( "StuckSaveFixer", "FixKey" );
		}

		private void Update()
		{
			if( !HUDItem.Get().m_Active && // Make sure RMB menu isn't open for any item right now
				!InputsManager.Get().m_TextInputActive && // Make sure chat isn't active
				GetButtonDown( hotkey ) && // Make sure hotkey is pressed
				( SaveGame.m_State == SaveGame.State.Save || SaveGame.m_State == SaveGame.State.SaveCoop ) ) // Make sure save is actually stuck
			{
				List<Item> itemsToRemove = new List<Item>( 4 );
				foreach( Item item in Item.s_AllItems )
				{
					if( !item )
					{
						if( item.m_Info == null )
							ModAPI.Log.Write( "=== Encountered item with null info" );
						else
							ModAPI.Log.Write( "=== Encountered item with info: " + item.m_Info.m_Type + " " + item.m_Info.m_FakeItem );

						itemsToRemove.Add( item );
					}
				}

				if( itemsToRemove.Count == 0 )
					ModAPI.Log.Write( "=== There were no invalid items!" );
				else
				{
					foreach( Item item in itemsToRemove )
						Item.s_AllItems.Remove( item );

					ModAPI.Log.Write( "=== Remove invalid items: " + itemsToRemove.Count );
				}

				SaveGame.m_State = SaveGame.State.None;
			}
		}

		// Check if hotkey is pressed this frame
		private bool GetButtonDown( KeyCode[] keys )
		{
			if( keys.Length == 0 )
				return false;

			// Check if modifier keys are all held
			for( int i = 0; i < keys.Length - 1; i++ )
			{
				if( !Input.GetKey( keys[i] ) )
					return false;
			}

			return Input.GetKeyDown( keys[keys.Length - 1] );
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