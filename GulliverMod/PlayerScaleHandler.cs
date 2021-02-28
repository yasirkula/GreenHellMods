using Enums;
using System.Collections.Generic;
using UnityEngine;

namespace GreenHell_GulliverMod
{
	// Make sure peers' CharacterControllers have the correct 'center' and 'height' values on this client
	public class ReplicatedPlayerTransformExtended : ReplicatedPlayerTransform
	{
		// This code is a copy&paste of the original implementation; only the "center" and "height" calculations
		// at the end are multiplied by the peer Player's scale
		protected override void Update()
		{
			if( !GulliverMod.IsEnabled )
				base.Update();
			else if( ReplIsOwner() )
			{
				if( m_NetTransform && m_LocalPlayerTransform )
					m_NetTransform.SetPositionAndRotation( m_LocalPlayerTransform.position, m_LocalPlayerTransform.rotation );

				m_IsSwimming = Player.Get().m_SwimController.IsActive();
			}
			else if( m_IsSwimming )
			{
				float scale = transform.localScale.x;
				CharacterController characterController = GetComponent<CharacterController>();
				Vector3 center = characterController.center;
				center.y = 1.35f * scale;
				characterController.center = center;
				characterController.height = 0.9f * scale;
			}
		}
	}

	public class PlayerExtended : Player
	{
		// Don't reset character controller values to hardcoded "0.9 center, 1.8 height" values while Gulliver is active.
		// This code is a copy&paste of the original implementation; only the "center" and "targetHeight" calculations
		// are multiplied by the Player's scale
		public override void UpdateCharacterControllerSizeAndCenter()
		{
			if( !GulliverMod.IsEnabled )
				base.UpdateCharacterControllerSizeAndCenter();
			else
			{
				float scale = transform.localScale.x;
				Vector3 center = m_CharacterController.center;
				m_CharacterControllerLastOffset = center;
				float targetHeight;
				if( m_SwimController.IsActive() )
				{
					center.y = 1.35f * scale;
					targetHeight = 0.9f * scale;
				}
				else if( m_FPPController.IsDuck() )
				{
					center.y = 0.65f * scale;
					targetHeight = 1.3f * scale;
				}
				else
				{
					center.y = 0.9f * scale;
					targetHeight = 1.8f * scale;
				}

				center.y = m_CharacterController.center.y + (float) ( ( center.y - (double) m_CharacterController.center.y ) * Time.deltaTime * 2.0 );
				if( m_FPPController.IsActive() && !FreeHandsLadderController.Get().IsActive() )
					center.z = m_LookController.m_LookDev.y > -40f ? CJTools.Math.GetProportionalClamp( 0.35f * scale, -0.35f * scale, m_LookController.m_LookDev.y, -40f, 80f ) : 0.35f * scale;

				m_CharacterController.height = m_CharacterController.height + (float) ( ( targetHeight - (double) m_CharacterController.height ) * Time.deltaTime * 2.0 );
				m_CharacterController.center = center;
				m_CharacterControllerDelta = center - m_CharacterControllerLastOffset;
				m_CharacterControllerDelta.y = 0f;
			}
		}

		// Increase fall distance proportional to scale
		protected override void OnLand( float fall_height )
		{
			if( GulliverMod.IsEnabled )
				fall_height /= Mathf.Max( 1f, Mathf.Pow( transform.localScale.x, 2f ) );

			base.OnLand( fall_height );
		}

		// When a weapon is equipped while the player was not at normal scale, weapon's size wouldn't match player's size. This function fixes it
		// This code is a copy&paste of the original implementation; we just reset player's scale to 1 temporarily before attaching item to its hand
		public override void AttachItemToHand( Hand hand, Item item )
		{
			if( !GulliverMod.IsEnabled )
				base.AttachItemToHand( hand, item );
			else
			{
				Vector3 playerScale = transform.localScale;
				transform.localScale = Vector3.one;

				Transform handTransform = hand == Hand.Left ? m_LHand : m_RHand;
				Quaternion quaternion = Quaternion.Inverse( item.m_Holder.localRotation );
				item.gameObject.transform.rotation = handTransform.rotation;
				item.gameObject.transform.rotation *= quaternion;
				Vector3 vector3 = item.transform.position - item.m_Holder.position;
				item.gameObject.transform.position = handTransform.position;
				item.gameObject.transform.position += vector3;
				item.gameObject.transform.parent = handTransform.transform;
				item.OnItemAttachedToHand();
				Physics.IgnoreCollision( m_Collider, item.m_Collider, true );

				transform.localScale = playerScale;
			}
		}

		// When a shrunk player drops item(s) from his hands, those items wouldn't scale up to the original scale
		// on other players. This function fixes it
		protected override void DetachItemFromHand( Item item )
		{
			if( GulliverMod.IsEnabled && GulliverMod.Instance && item && !ReplTools.IsPlayingAlone() )
			{
				Transform itemParent = item.transform.parent;
				if( itemParent && ( itemParent == m_LHand.transform || itemParent == m_RHand.transform ) )
				{
					if( item.m_Info.IsHeavyObject() )
					{
						List<GameObject> droppedItems = new List<GameObject>( ( (HeavyObject) item ).m_Attached.Count + 1 ) { item.gameObject };
						foreach( KeyValuePair<Transform, HeavyObject> kvPair in ( (HeavyObject) item ).m_Attached )
						{
							if( kvPair.Key && kvPair.Value )
								droppedItems.Add( kvPair.Value.gameObject );
						}

						GulliverMod.Instance.OnPlayerDroppedItems( droppedItems.ToArray() );
					}
					else
						GulliverMod.Instance.OnPlayerDroppedItems( new GameObject[1] { item.gameObject } );
				}
			}

			base.DetachItemFromHand( item );
		}
	}

	// Make sure player swims at the correct y-level regardless of his scale
	public class SwimControllerExtended : SwimController
	{
		protected override void Update()
		{
			m_PlayerPosOffset = 1.55f * m_Player.transform.localScale.x;
			base.Update();
		}
	}

	// Make sure player sees the map at the correct scale (it won't affect peers, though)
	public class MapControllerExtended : MapController
	{
		protected override void CreateMapObject()
		{
			base.CreateMapObject();

			if( m_Map )
				m_Map.transform.localScale *= m_Player.transform.localScale.x;
		}
	}

	// Make sure player sees the notepad at the correct scale (it won't affect peers, though)
	public class NotepadControllerExtended : NotepadController
	{
		protected override void CreateNotepadObject()
		{
			base.CreateNotepadObject();

			if( m_Notepad )
				m_Notepad.transform.localScale *= m_Player.transform.localScale.x;
		}
	}

	// WeaponSpearController changes Animator's speed depending on Player.Get().m_SpeedMul, so it overrides our changes.
	// We must override its overrides with our changes again
	public class WeaponSpearControllerExtended : WeaponSpearController
	{
		protected override void OnDisable()
		{
			base.OnDisable();
			ResetAnimatorSpeedToGulliver();
		}

		protected void Update()
		{
			ResetAnimatorSpeedToGulliver();
		}

		protected void LateUpdate()
		{
			ResetAnimatorSpeedToGulliver();
		}

		private void ResetAnimatorSpeedToGulliver()
		{
			if( GulliverMod.IsEnabled )
			{
				PlayerScaleHandler.UpdateAnimatorSpeed( m_Animator, Player.Get().transform.localScale.x );

				if( IsAttack() )
					m_Animator.speed *= Skill.Get<SpearFishingSkill>().GetAnimationSpeedMul();
			}
		}
	}

	// Make sure worms and leeches that spawn on player have the correct scale
	// Looks like this is no longer needed. Maybe ItemExtended.UpdateScale fixed the issue?
	//public class BIWoundSlotExtended : BIWoundSlot
	//{
	//	public override void SetInjury( Injury injury )
	//	{
	//		base.SetInjury( injury );

	//		if( GulliverMod.IsEnabled &&
	//			injury != null && ( injury.m_Type == InjuryType.Worm || injury.m_Type == InjuryType.Leech ) &&
	//			m_Transform && m_Wound && m_Wound != m_Transform )
	//		{
	//			ScaleLeechWithGulliverPlayer();
	//		}
	//	}

	//	// If we don't create a new function for this single line of code, ModAPI fails to inject the function
	//	// (Game will throw "InvalidProgramException: Invalid IL code in BIWoundSlot:SetInjury" at runtime
	//	// and opening the compiled code with a decompiler will fail to decompile the SetInjury function)
	//	// That's absurd! I'm really tired of being afraid to use Dictionary.TryGetValue, try-catch blocks or
	//	// early return statements in my functions. ModAPI will fail miserably in some of these conditions and
	//	// it is really annoying
	//	public void ScaleLeechWithGulliverPlayer()
	//	{
	//		m_Wound.transform.localScale = Vector3.one;
	//	}
	//}

	// Make sure arrows have the correct scale (apparently not needed?)
	//public class BowControllerExtended : BowController
	//{
	//	protected override void UpdateArrow()
	//	{
	//		base.UpdateArrow();

	//		if( m_Arrow && m_Arrow.gameObject.activeSelf )
	//		{
	//			m_Arrow.UpdateScale();
	//			m_Arrow.transform.localScale *= m_Player.transform.localScale.x;
	//		}
	//	}

	//	protected override void UnequipLoadedArrow()
	//	{
	//		Arrow arrow = m_Arrow;
	//		base.UnequipLoadedArrow();

	//		if( !m_Arrow && arrow )
	//			arrow.UpdateScale();
	//	}
	//}

	// Make sure items always have the correct scale (at least on player's screen)
	public class ItemExtended : Item
	{
		public override void UpdateScale()
		{
			if( !GulliverMod.IsEnabled )
				base.UpdateScale();
			else
			{
				if( m_IsBeingDestroyed || !ReplIsOwner() )
					return;

				m_WantedScale = m_DefaultLocalScale;

				if( m_CurrentSlot && m_CurrentSlot.m_InventoryStackSlot )
					m_WantedScale = Vector3.one;
				else
				{
					if( m_CurrentSlot != null && m_CurrentSlot.m_IsHookBaitSlot )
						return;
					else if( Inventory3DManager.Get().m_StackItems.Contains( this ) )
						m_WantedScale = Vector3.one;
					else if( ( m_InInventory || m_InStorage ) && ( !m_CurrentSlot || !m_CurrentSlot.m_InventoryStackSlot ) || m_OnCraftingTable || Inventory3DManager.Get().m_CarriedItem == this )
						m_WantedScale = m_InventoryLocalScale;
					else if( m_CurrentSlot != null && ( m_CurrentSlot.m_ParentType == ItemSlot.ParentType.FoodProcessor || m_CurrentSlot.m_ParentType == ItemSlot.ParentType.WaterCollector || m_CurrentSlot.m_ParentType == ItemSlot.ParentType.WaterFilter ) )
						m_WantedScale = Vector3.one;
					else if( m_CurrentSlot != null && !m_CurrentSlot.m_InventoryStackSlot )
						m_WantedScale = m_InventoryLocalScale;
					else if( m_Info != null && m_Info.IsArrow() && ( (Arrow) (Item) this ).m_Loaded )
						m_WantedScale = Vector3.one;
				}

				if( m_WantedScale != transform.localScale )
				{
					transform.localScale = m_WantedScale;
					ReplSetDirty();
				}
			}
		}
	}

	// Reset player's scale on death
	public class DeathControllerExtended : DeathController
	{
		public override void Respawn()
		{
			if( GulliverMod.IsEnabled && GulliverMod.Instance )
				GulliverMod.Instance.SetPlayerScale( 1f );

			base.Respawn();
		}
	}

	// Reset player's scale on death
	public class HUDDeathExtended : HUDDeath
	{
		public override void OnLoadGame()
		{
			if( GulliverMod.IsEnabled && GulliverMod.Instance )
				GulliverMod.Instance.SetPlayerScale( 1f );

			base.OnLoadGame();
		}
	}

	// Keep track of changes to Player's scale and update player's scale-related properties while
	// notifying peers of this change in the meantime
	public class PlayerScaleHandler : MonoBehaviour
	{
		private enum ScaledPlayerState { Normal = 0, Ducking = 1, Swimming = 2 };

		private const float PLAYER_SCALE_ON_CHANGE_SYNC_INTERVAL = 0.25f;
		private const float PLAYER_SCALE_REGULAR_SYNC_INTERVAL = 5f;

		private float nextOnChangeScaleSyncTime, nextRegularScaleSyncTime;
		private float prevScale = 1f, remoteScale = 1f;

		private Transform targetTransform;
		private Animator targetAnimator;
		private CharacterController targetCharacterController;

		//private ScaledPlayerState scaledPlayerState = ScaledPlayerState.Normal;

		private void Start()
		{
			targetTransform = Player.Get().transform;
			targetAnimator = Player.Get().m_Animator;
			targetCharacterController = Player.Get().GetComponent<CharacterControllerProxy>().m_Controller;

			nextRegularScaleSyncTime = Time.realtimeSinceStartup + PLAYER_SCALE_REGULAR_SYNC_INTERVAL;
		}

		private void Update()
		{
			float scale = targetTransform.localScale.x;
			if( !Mathf.Approximately( scale, prevScale ) )
			{
				prevScale = scale;
				OnPlayerScaleChanged( scale );
			}
			//else if( !Mathf.Approximately( scale, 1f ) )
			//{
			//	// Update CharacterController's height and center values when player's state changes
			//	ScaledPlayerState _scaledPlayerState = GetScaledPlayerState();
			//	if( _scaledPlayerState != scaledPlayerState )
			//	{
			//		scaledPlayerState = _scaledPlayerState;
			//		UpdateCharacterController( scale );
			//	}
			//}

			if( Time.realtimeSinceStartup >= nextOnChangeScaleSyncTime )
			{
				nextOnChangeScaleSyncTime = Time.realtimeSinceStartup + PLAYER_SCALE_ON_CHANGE_SYNC_INTERVAL;

				// If Player's scale has changed and we aren't playing solo, notify other user's of this change
				if( !Mathf.Approximately( scale, remoteScale ) )
					SyncScale( false );
			}
			else if( Time.realtimeSinceStartup >= nextRegularScaleSyncTime )
			{
				// Send scale at a regular interval even if it hasn't changed in case a new user joins or UDP fails
				nextRegularScaleSyncTime = Time.realtimeSinceStartup + PLAYER_SCALE_REGULAR_SYNC_INTERVAL;
				SyncScale( true );
			}
		}

		private void OnPlayerScaleChanged( float scale )
		{
			//scaledPlayerState = GetScaledPlayerState();

			UpdateAnimatorSpeed( targetAnimator, scale );
			UpdateCharacterController( scale );

			// "transform" here isn't the Player itself, it is Player's remote representation that is synced with peers (P2PPlayer)
			transform.localScale = new Vector3( scale, scale, scale );

			Player.Get().m_SpeedMul = GulliverMod.GetMultipliedScale( scale, 0.5f );
			Player.DEEP_WATER = 1.8f * scale;

			if( CameraManager.Get() && CameraManager.Get().m_MainCamera )
				CameraManager.Get().m_MainCamera.nearClipPlane = 0.01f * scale;
		}

		//private ScaledPlayerState GetScaledPlayerState()
		//{
		//	if( Player.Get().m_SwimController.IsActive() )
		//		return ScaledPlayerState.Swimming;
		//	if( Player.Get().IsDuck() )
		//		return ScaledPlayerState.Ducking;

		//	return ScaledPlayerState.Normal;
		//}

		private void UpdateCharacterController( float scale )
		{
			Player.Get().UpdateCharacterControllerSizeAndCenter();
			targetCharacterController.radius = 0.35f * scale;
			targetCharacterController.stepOffset = 0.3f * scale;
		}

		private void SyncScale( bool tcp )
		{
			if( !ReplTools.IsPlayingAlone() )
			{
				float scale = targetTransform.localScale.x;
				remoteScale = scale;

				P2PNetworkWriter writer = new P2PNetworkWriter();
				writer.StartMessage( GulliverMod.PLAYER_SCALE_SYNC_MESSAGE_TYPE );
				writer.Write( gameObject );
				writer.Write( scale );
				writer.FinishMessage();
				P2PSession.Instance.SendWriterToAll( writer, tcp ? 0 : 1 ); // 0: TCP, 1: UDP
			}
		}

		public static void UpdateAnimatorSpeed( Animator animator, float scale )
		{
			animator.speed = Mathf.Max( 1f / GulliverMod.GetMultipliedScale( scale, 0.25f ), 0.5f );
		}
	}
}