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

		private void Awake()
		{
			dropItemToTableClip = Resources.Load( "Sounds/Items/click_drop_item_backpack" ) as AudioClip;
		}

		private void Update()
		{
			// If Z or X keys are pressed while hovering an item in inventory, move the item to crafting table
			Inventory3DManager inventory = Inventory3DManager.Get();
			CraftingManager craftingTable = CraftingManager.Get();
			
			if( inventory && craftingTable && inventory.IsActive() && inventory.m_FocusedItem && !inventory.m_FocusedItem.m_OnCraftingTable &&
				!inventory.m_CarriedItem && inventory.CanSetCarriedItem( true ) &&
				TriggerController.Get().GetBestTrigger() && TriggerController.Get().GetBestTrigger().gameObject == inventory.m_FocusedItem.gameObject &&
				!HUDItem.Get().m_Active &&
				( Input.GetKeyDown( KeyCode.Z ) || Input.GetKeyDown( KeyCode.X ) ) )
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
	}
}