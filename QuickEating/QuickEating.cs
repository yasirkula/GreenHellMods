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
			if( id == AnimEventID.GrabItem && m_TriggerActionToExecute == TriggerAction.TYPE.Take && m_TriggerToExecute && QuickEating.heldInteractionKey &&
				( ( m_TriggerToExecute is ItemReplacer && ( (ItemReplacer) m_TriggerToExecute ).m_ReplaceInfo.m_Eatable ) ||
				  ( m_TriggerToExecute is PlantFruit && ( (PlantFruit) m_TriggerToExecute ).m_ItemInfo.m_Eatable ) ||
				  ( m_TriggerToExecute is Item && ( (Item) m_TriggerToExecute ).m_Info.m_Eatable ) ) )
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
					item.Eat();
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
				!HUDItem.Get().m_Active &&
				Input.GetKeyDown( GetActionKeyCode() ) )
			{
				if( !inventory.m_FocusedItem.ReplIsOwner() )
					inventory.m_FocusedItem.ReplRequestOwnership();

				inventory.m_FocusedItem.ReplSetDirty();
				inventory.m_FocusedItem.Eat();
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