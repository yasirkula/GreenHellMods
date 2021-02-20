using UnityEngine;
using Enums;

namespace GreenHell_QuickEating
{
	public class AddMyGameObject : Player
	{
		// Create an instance of QuickEating
		protected override void Start()
		{
			base.Start();
			new GameObject( "__QuickEating__" ).AddComponent<QuickEating>();
		}
	}

	public class MainMenuOptionsControlsKeysBindingExtended : MainMenuOptionsControlsKeysBinding
	{
		// Key bindings have changed, reset ACTION binding's cached KeyCode
		protected override void ApplyOptions()
		{
			QuickEating.interactionKey = null;
			base.ApplyOptions();
		}
	}

	public class TriggerControllerExtended : TriggerController
	{
		public override void OnAnimEvent( AnimEventID id )
		{
			// If a food is grabbed from the ground or a tree and the ACTION key has been held for more than 0.5 seconds,
			// eat the food instead of sending it to inventory
			ItemInfo itemInfo = null;
			if( id == AnimEventID.GrabItem && m_TriggerActionToExecute == TriggerAction.TYPE.Take && m_TriggerToExecute && QuickEating.heldInteractionKey )
			{
				if( m_TriggerToExecute is ItemReplacer )
					itemInfo = ( (ItemReplacer) m_TriggerToExecute ).m_ReplaceInfo;
				else if( m_TriggerToExecute is PlantFruit )
					itemInfo = ( (PlantFruit) m_TriggerToExecute ).m_ItemInfo;
				else if( m_TriggerToExecute is Item )
					itemInfo = ( (Item) m_TriggerToExecute ).m_Info;
			}

			if( itemInfo != null && ( itemInfo.m_Eatable || itemInfo.m_Drinkable ) )
			{
				Trigger trigger = m_TriggerToExecute;
				m_TriggerActionToExecute = TriggerAction.TYPE.None;
				m_LastTrigerExecutionTime = Time.time;
				m_TriggerToExecute = null;

				Item item = null;
				if( trigger is ItemReplacer )
					item = ( (ItemReplacer) trigger ).ReplaceItem();
				else if( trigger is Item )
					item = (Item) trigger;

				if( item )
				{
					if( !item.ReplIsOwner() )
						item.ReplRequestOwnership();

					item.ReplSetDirty();

					if( itemInfo.m_Eatable )
						item.Eat();
					else
						item.Drink();
				}
				else
					( (PlantFruit) trigger ).Eat();
			}
			else
				base.OnAnimEvent( id ); // Send item to inventory
		}
	}

	public class QuickEating : MonoBehaviour
	{
		private float interactionKeyHeldTime = 0f;
		public static KeyCode? interactionKey = null;
		public static bool heldInteractionKey = false;

		private void Update()
		{
			// Check if ACTION key is held for more than 0.5 seconds
			if( Input.GetKeyUp( GetActionKeyCode() ) )
			{
				interactionKeyHeldTime = 0f;
				heldInteractionKey = false;
			}
			else if( !heldInteractionKey && Input.GetKey( GetActionKeyCode() ) )
			{
				interactionKeyHeldTime += Time.deltaTime;
				if( interactionKeyHeldTime >= 0.5f )
					heldInteractionKey = true;
			}

			// If ACTION key is pressed while hovering a food in inventory, eat it
			Inventory3DManager inventory = Inventory3DManager.Get();
			if( inventory && inventory.IsActive() && inventory.m_FocusedItem && !inventory.m_FocusedItem.m_OnCraftingTable && !inventory.m_CarriedItem &&
				TriggerController.Get().GetBestTrigger() && TriggerController.Get().GetBestTrigger().gameObject == inventory.m_FocusedItem.gameObject &&
				!HUDItem.Get().m_Active && ( inventory.m_FocusedItem.m_Info.m_Eatable || inventory.m_FocusedItem.m_Info.m_Drinkable ) &&
				Input.GetKeyDown( GetActionKeyCode() ) )
			{
				if( !inventory.m_FocusedItem.ReplIsOwner() )
					inventory.m_FocusedItem.ReplRequestOwnership();

				inventory.m_FocusedItem.ReplSetDirty();

				if( inventory.m_FocusedItem.m_Info.m_Eatable )
					inventory.m_FocusedItem.Eat();
				else
					inventory.m_FocusedItem.Drink();
			}
		}

		// Returns KeyCode of ACTION key binding
		private KeyCode GetActionKeyCode()
		{
			if( interactionKey == null )
			{
				InputActionData dataByTriggerAction = InputsManager.Get().GetActionDataByTriggerAction( TriggerAction.TYPE.Take, ControllerType._Count );
				interactionKey = dataByTriggerAction != null ? dataByTriggerAction.m_KeyCode : KeyCode.E;
			}

			return interactionKey.Value;
		}
	}
}