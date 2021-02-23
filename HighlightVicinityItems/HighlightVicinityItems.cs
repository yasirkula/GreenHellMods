using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreenHell_HighlightVicinityItems
{
	public class AddMyGameObject : Player
	{
		// Create an instance of HighlightVicinityItems
		protected override void Start()
		{
			base.Start();
			new GameObject( "__HighlightVicinityItems__" ).AddComponent<HighlightVicinityItems>();
		}
	}

	public class ReadableItemExtended : ReadableItem
	{
		public override void UpdateLayer()
		{
			// ReadableItem's built-in implementation ignores m_ForcedLayer, handle highlight manually
			if( HighlightVicinityItems.HighlightedItems.Contains( this ) )
			{
				if( gameObject.layer != m_OutlineLayer )
					SetLayer( transform, m_OutlineLayer );
			}
			else
				base.UpdateLayer();
		}
	}

	public class HighlightVicinityItems : MonoBehaviour
	{
		private const float MIN_RADIUS = 0.5f;
		private const float MAX_RADIUS = 50f;
		private const float UPDATE_INTERVAL = 0.25f;

		private KeyCode[] hotkey;
		private float radius = 15f;

		private bool isEnabled = false;
		private float nextUpdateTime;

		public static readonly HashSet<Trigger> HighlightedItems = new HashSet<Trigger>();

		private void Start()
		{
			hotkey = GetConfigurableKey( "HighlightVicinityItems", "ToggleKey" );
		}

		private void OnDisable()
		{
			HighlightedItems.Clear();
		}

		private void Update()
		{
			// If the configurable hotkey is pressed, toggle highlights
			if( !HUDItem.Get().m_Active && // Make sure RMB menu isn't open for any item right now
				!InputsManager.Get().m_TextInputActive ) // Make sure chat isn't active
			{
				if( GetButtonDown( hotkey ) ) // If hotkey is pressed
				{
					isEnabled = !isEnabled;
					if( !isEnabled )
					{
						// Clear highlights
						Trigger[] _highlightedItems = new Trigger[HighlightedItems.Count];
						HighlightedItems.CopyTo( _highlightedItems );
						HighlightedItems.Clear();

						for( int i = 0; i < _highlightedItems.Length; i++ )
						{
							if( _highlightedItems[i] )
								_highlightedItems[i].m_ForcedLayer = 0;
						}
					}
				}

				if( isEnabled && Input.GetKey( KeyCode.LeftControl ) )
				{
					float scrollDelta = Input.mouseScrollDelta.y;
					if( scrollDelta != 0f )
						radius = Mathf.Clamp( radius + scrollDelta, MIN_RADIUS, MAX_RADIUS );
				}
			}

			if( isEnabled && Time.time >= nextUpdateTime )
			{
				nextUpdateTime = Time.time + UPDATE_INTERVAL;

				Vector3 playerPos = Player.Get().transform.position;
				float rangeSqr = radius * radius;

				// Find items within radius
				foreach( Trigger trigger in Trigger.s_ActiveTriggers )
				{
					if( !trigger )
						continue;

					Item item = trigger as Item;
					if( item )
					{
						// Don't highlight trees
						if( item.m_IsTree )
							continue;

						// Don't highlight useless plants
						if( item.m_IsPlant )
						{
							switch( item.m_Info.m_ID )
							{
								case Enums.ItemID.small_plant_08_cut:
								case Enums.ItemID.small_plant_10_cut:
								case Enums.ItemID.small_plant_13_cut:
								case Enums.ItemID.small_plant_14_cut:
								case Enums.ItemID.medium_plant_02_cut:
								case Enums.ItemID.medium_plant_04_cut:
								case Enums.ItemID.medium_plant_10_cut: break;
								default: continue;
							}
						}
					}

					bool isInRange = trigger.transform.position.Distance2DSqr( playerPos ) <= rangeSqr;
					if( isInRange )
					{
						if( !HighlightedItems.Contains( trigger ) )
							HighlightedItems.Add( trigger );

						if( trigger.m_ForcedLayer != trigger.m_OutlineLayer )
							trigger.m_ForcedLayer = trigger.m_OutlineLayer;
					}
					else if( HighlightedItems.Contains( trigger ) )
					{
						HighlightedItems.Remove( trigger );
						trigger.m_ForcedLayer = 0;
					}
				}

				// Remove destroyed items from HashSet
				HighlightedItems.RemoveWhere( ( trigger ) => !trigger );
			}
		}

		private void OnGUI()
		{
			if( isEnabled )
				GUI.Label( new Rect( Screen.width - 210f, 10f, 200f, 35f ), "Highlighting items within vicinity: " + radius );
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