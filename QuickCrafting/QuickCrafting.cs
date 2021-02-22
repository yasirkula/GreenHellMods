using System.Collections.Generic;
using UnityEngine;

namespace GreenHell_QuickCrafting
{
	public class AddMyGameObject : Player
	{
		// Create an instance of QuickCrafting
		protected override void Start()
		{
			base.Start();
			new GameObject( "__QuickCrafting__" ).AddComponent<QuickCrafting>();
		}
	}

	public class QuickCrafting : MonoBehaviour
	{
		private KeyCode[] hotkey;
		private bool openInventoryIfNotOpen = false;

		private AudioClip dropItemToTableClip;

		private void Start()
		{
			hotkey = GetConfigurableKey( "QuickCrafting", "CraftKey" );

			foreach( KeyCode key in GetConfigurableKey( "QuickCrafting", "ForceOpenInv" ) )
			{
				if( key == KeyCode.Y )
					openInventoryIfNotOpen = true;
			}

			dropItemToTableClip = Resources.Load( "Sounds/Items/click_drop_item_backpack" ) as AudioClip;
		}

		private void Update()
		{
			// If the configurable hotkey is pressed while hovering an item in inventory, move the item to crafting table
			Inventory3DManager inventory = Inventory3DManager.Get();
			CraftingManager craftingTable = CraftingManager.Get();

			if( inventory && craftingTable &&
				!HUDItem.Get().m_Active && // Make sure RMB menu isn't open for any item right now
				TriggerController.Get().GetBestTrigger() && // Make sure there is a highlighted item
				!InputsManager.Get().m_TextInputActive && // Make sure chat isn't active
				GetButtonDown( hotkey ) ) // Make sure hotkey is pressed
			{
				bool forceOpenedInventory = false;
				if( !inventory.IsActive() && openInventoryIfNotOpen )
				{
					// Force open inventory
					Item triggerItem = TriggerController.Get().GetBestTrigger().GetComponent<Item>();
					if( triggerItem )
					{
						inventory.Activate();
						if( inventory.IsActive() )
						{
							forceOpenedInventory = true;

							inventory.m_FocusedItem = triggerItem;
							if( !triggerItem.GetWasTriggered() )
								triggerItem.SetWasTriggered( true );
						}
					}
				}

				if( inventory.IsActive() && // Make sure inventory is currently open
					inventory.m_FocusedItem && !inventory.m_FocusedItem.m_OnCraftingTable && // Make sure the highlighted item isn't already on crafting table
					!inventory.m_CarriedItem && inventory.CanSetCarriedItem( true ) && // Make sure we aren't drag & dropping any items at the moment
					TriggerController.Get().GetBestTrigger().gameObject == inventory.m_FocusedItem.gameObject ) // Make sure the highlighted item is the item that the cursor is on
				{
					craftingTable.Activate();

					inventory.StartCarryItem( inventory.m_FocusedItem, false );
					craftingTable.AddItem( inventory.m_CarriedItem, true );

					if( inventory.m_StackItems != null )
					{
						for( int i = 0; i < inventory.m_StackItems.Count; i++ )
							craftingTable.AddItem( inventory.m_StackItems[i], true );
					}

					inventory.SetCarriedItem( null, true );

					if( dropItemToTableClip )
						inventory.GetComponent<AudioSource>().PlayOneShot( dropItemToTableClip );
				}
				else if( forceOpenedInventory )
					inventory.Deactivate();
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