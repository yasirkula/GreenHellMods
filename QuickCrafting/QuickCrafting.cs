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
		private AudioClip dropItemToTableClip;
		private KeyCode hotkey = KeyCode.X;

		private void Start()
		{
			dropItemToTableClip = Resources.Load( "Sounds/Items/click_drop_item_backpack" ) as AudioClip;
			hotkey = GetConfigurableKey( "QuickCrafting", "CraftKey", hotkey );
		}

		private void Update()
		{
			// If the configurable hotkey is pressed while hovering an item in inventory, move the item to crafting table
			Inventory3DManager inventory = Inventory3DManager.Get();
			CraftingManager craftingTable = CraftingManager.Get();

			if( inventory && craftingTable && inventory.IsActive() && inventory.m_FocusedItem && !inventory.m_FocusedItem.m_OnCraftingTable &&
				!inventory.m_CarriedItem && inventory.CanSetCarriedItem( true ) &&
				TriggerController.Get().GetBestTrigger() && TriggerController.Get().GetBestTrigger().gameObject == inventory.m_FocusedItem.gameObject &&
				!HUDItem.Get().m_Active && Input.GetKeyDown( hotkey ) )
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
		}

		// Returns configurable key's corresponding KeyCode by parsing RuntimeConfiguration.xml
		private KeyCode GetConfigurableKey( string modID, string keyID, KeyCode defaultValue )
		{
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
							string keyStr = configuration.Substring( keyTagStart + keyTag.Length, keyTagEnd - keyTagStart - keyTag.Length );
							if( keyStr.Length > 0 && System.Enum.IsDefined( typeof( KeyCode ), keyStr ) )
								return (KeyCode) System.Enum.Parse( typeof( KeyCode ), keyStr );
						}
					}
				}
			}

			return defaultValue;
		}
	}
}