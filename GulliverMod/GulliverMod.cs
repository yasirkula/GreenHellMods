using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Following custom message types are used by this mod (peers without this mod will ignore these messages):
// 173: to tell peers whether or not the mod is enabled. Parameters:
//      - a single 'bool' storing host's IsEnabled state
// 174: to transmit a player's Transform scale changes to peers. Parameters:
//      - the changed Player 'GameObject' followed by its uniform 'float scale'
// 175: to transmit entity (AI) Transform scale changes to peers. Parameters:
//      - 'byte count' indicating how many scale changes are encoded in the message
//      - for each encoded scale change, the changed 'GameObject' followed by its uniform 'float scale'
// 176: to request cached entity (AI) Transform scale values from the host (called by players that join the server). Takes no parameters
// 177: to transmit items dropped by a player to peers (so that they can correct the dropped items' scales). Parameters:
//      - 'byte count' indicating how many items are dropped
//      - each dropped item's 'GameObject'
namespace GreenHell_GulliverMod
{
	public class AddMyGameObject : Player
	{
		protected override void Start()
		{
			base.Start();

			// Create an instance of GulliverMod
			new GameObject( "__GulliverMod__" ).AddComponent<GulliverMod>();

			// If clear flags is left as None and the player shrinks, trees on the horizon start appearing in front of real trees
			if( CameraManager.Get() && CameraManager.Get().m_MainCamera )
				CameraManager.Get().m_MainCamera.clearFlags = CameraClearFlags.Depth;
		}
	}

	// Get notification when the player is about to leave the server
	public class P2PSessionExtended : P2PSession
	{
		public override void Stop()
		{
			if( GulliverMod.Instance )
				GulliverMod.Instance.OnAboutToLeaveServer();

			base.Stop();
		}
	}

	// If an AI was scaled by a player and that AI came from this spawner, make sure that the next AI this
	// spawner spawns will also have the same scale. This way, if player scales an AI but that AI runs
	// away, when player returns to the spawner he will find another AI with the same scale
	public class AISpawnerExtended : AIs.AISpawner
	{
		protected override void SpawnObject( Vector3 position )
		{
			base.SpawnObject( position );

			if( GulliverMod.IsEnabled && m_AIs.Count > 0 && m_AIs[m_AIs.Count - 1] && GulliverMod.Instance && GulliverMod.Instance.AISpawnerEntityScales.ContainsKey( this ) )
			{
				// Set one of the spawned entities' scale to the custom scale
				float customEntityScale = GulliverMod.Instance.AISpawnerEntityScales[this];
				bool customScaleEntityAlreadySpawned = false;
				for( int i = 0; i < m_AIs.Count - 1; i++ )
				{
					if( m_AIs[i] && Mathf.Approximately( m_AIs[i].transform.localScale.x, customEntityScale ) )
					{
						customScaleEntityAlreadySpawned = true;
						break;
					}
				}

				if( !customScaleEntityAlreadySpawned )
				{
					Transform entity = m_AIs[m_AIs.Count - 1].transform;
					entity.localScale = new Vector3( customEntityScale, customEntityScale, customEntityScale );
					GulliverMod.Instance.OnScaledEntitySpawned( entity );
				}
			}
		}
	}

	// Make sure scaled entities' animation speeds also change (doesn't always work in multiplayer, though)
	public class AnimationModuleExtended : AIs.AnimationModule
	{
		private float gulliverSpeedMultiplier;

		protected override void Start()
		{
			if( Mathf.Approximately( gulliverSpeedMultiplier, 0f ) )
				gulliverSpeedMultiplier = 1f;

			base.Start();
		}

		public override void OnUpdate()
		{
			if( !GulliverMod.IsEnabled )
				base.OnUpdate();
			else
				UpdateAnimatorGulliver();
		}

		// This code is a copy&paste of the original implementation; only the animator speed calculations at the end
		// are multiplied by entity's scale
		private void UpdateAnimatorGulliver()
		{
			if( !m_ParamsInited )
				InitParams();

			UpdateBlend();
			UpdateAttackBlend();

			if( m_AI.m_Animator.IsInTransition( 0 ) || m_WantedAnim.Length <= 0 )
				return;
			if( m_CurrentAnim != m_WantedAnim )
			{
				m_CurrentAnim = m_WantedAnim;
				float fixedTimeOffset = 0.0f;
				if( m_StartFromRandomFrame )
				{
					fixedTimeOffset = Random.Range( 0.0f, m_StatesData[m_CurrentAnim].m_Duration );
					m_StartFromRandomFrame = false;
				}
				if( m_PrevAnim == m_CurrentAnim && m_StatesData[m_CurrentAnim].m_Loop )
				{
					m_TransitionDuration = DEFAULT_TRANSITION_DURATION;
					return;
				}

				m_AI.m_Animator.CrossFadeInFixedTime( m_CurrentAnim, m_TransitionDuration, -1, fixedTimeOffset );
				m_TransitionDuration = DEFAULT_TRANSITION_DURATION;
				m_PrevAnim = m_CurrentAnim;
			}

			AnimatorStateInfo animatorStateInfo = m_AI.m_Animator.GetCurrentAnimatorStateInfo( 0 );
			if( m_ForcedSpeed >= 0.0 )
				m_AI.m_Animator.speed = m_ForcedSpeed * gulliverSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_SneakHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_SneakSpeedMul * gulliverSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_WalkHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_WalkSpeedMul * gulliverSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_TrotHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_TrotSpeedMul * gulliverSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_RunHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_RunSpeedMul * gulliverSpeedMultiplier;
			else
				m_AI.m_Animator.speed = gulliverSpeedMultiplier;
		}
	}

	// Make sure scaled human entities' animation speeds also change (doesn't always work in multiplayer, though)
	public class HumanAnimationModuleExtended : AIs.HumanAnimationModule
	{
		private float gulliverHumanSpeedMultiplier;

		protected override void Start()
		{
			if( Mathf.Approximately( gulliverHumanSpeedMultiplier, 0f ) )
				gulliverHumanSpeedMultiplier = 1f;

			base.Start();
		}

		public override void OnUpdate()
		{
			if( !GulliverMod.IsEnabled )
				base.OnUpdate();
			else
				UpdateHumanAnimatorGulliver();
		}

		// This code is a copy&paste of the original implementation; only the animator speed calculations at the end
		// are multiplied by entity's scale
		private void UpdateHumanAnimatorGulliver()
		{
			if( m_WantedAnim.Length <= 0 )
				return;
			if( m_CurrentAnim != m_WantedAnim )
			{
				m_CurrentAnim = m_WantedAnim;
				float fixedTimeOffset = 0.0f;
				if( m_StartFromRandomFrame )
				{
					fixedTimeOffset = Random.Range( 0.0f, m_StatesData[m_CurrentAnim].m_Duration );
					m_StartFromRandomFrame = false;
				}
				if( m_PrevAnim == m_CurrentAnim && m_StatesData.ContainsKey( m_CurrentAnim ) && m_StatesData[m_CurrentAnim].m_Loop )
				{
					m_TransitionDuration = DEFAULT_TRANSITION_DURATION;
					return;
				}

				m_AI.m_Animator.CrossFadeInFixedTime( m_CurrentAnim, m_TransitionDuration, -1, fixedTimeOffset );
				m_TransitionDuration = DEFAULT_TRANSITION_DURATION;
				m_PrevAnim = m_CurrentAnim;
			}

			AnimatorStateInfo animatorStateInfo = m_AI.m_Animator.GetCurrentAnimatorStateInfo( 0 );
			if( m_ForcedSpeed >= 0.0 )
				m_AI.m_Animator.speed = m_ForcedSpeed * gulliverHumanSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_WalkHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_WalkSpeedMul * gulliverHumanSpeedMultiplier;
			else if( animatorStateInfo.shortNameHash == m_RunHash )
				m_AI.m_Animator.speed = m_AI.m_Params.m_RunSpeedMul * gulliverHumanSpeedMultiplier;
			else
				m_AI.m_Animator.speed = gulliverHumanSpeedMultiplier;
		}
	}

	public class GulliverMod : MonoBehaviour
	{
		private struct EntityData
		{
			public readonly Transform transform;
			public readonly Behaviour animator;
			public readonly float invDefaultScale;

			public EntityData( Transform entity )
			{
				transform = entity;
				animator = entity.GetComponent<AIs.AnimationModule>();
				if( !animator )
					animator = entity.GetComponent<Animator>();

				invDefaultScale = animator ? ( 1f / GetDefaultScale( entity ) ) : 1f;
			}

			public void SetSpeedMultiplier( float multiplier )
			{
				// Can't access injected fields, so we must use reflection. Bruh...
				if( animator is AIs.HumanAnimationModule )
					aiHumanAnimationSpeed.SetValue( animator, multiplier );
				else if( animator is AIs.AnimationModule )
					aiAnimationSpeed.SetValue( animator, multiplier );
				else
					( (Animator) animator ).speed = multiplier;
			}
		}

		private const int WINDOW_ID = 300442;

		// Animal AIs reside on layer 19, human AIs reside on layer 0
		private const int AI_LAYER_MASK = ( 1 << 19 ) | ( 1 << 0 );

		// Scale values in meters (player's scale values should be multiplied by 1.8 for meters)
		private const float MIN_ENTITY_SCALE = 0.25f;
		private const float MAX_ENTITY_SCALE = 5f;
		private const float MIN_PLAYER_SCALE = 0.1f;
		private const float MAX_PLAYER_SCALE = 10f;

		// Scaling speed via numpad - and + keys
		private const float KEYBOARD_SCALE_SPEED = 1f;

		// Maximum number of scaled-AIs that will be cached in order to synchronize them with new users
		private const int MAX_NUMBER_OF_SCALED_ENTITIES_TO_CACHE = 16;

		// Sync interval of host's IsEnabled variable (whether or not host has activated the mod)
		private const float ENABLED_STATE_SYNC_INTERVAL = 5f;

		// Sync interval to broadcast latest changes to entities' scales
		private const float MODIFIED_ENTITIES_SYNC_INTERVAL = 0.33f;

		// Custom message types
		public const byte ENABLED_STATE_SYNC_MESSAGE_TYPE = 173;
		public const byte PLAYER_SCALE_SYNC_MESSAGE_TYPE = 174;
		public const byte ENTITY_SCALE_SYNC_MESSAGE_TYPE = 175;
		public const byte ENTITY_SCALE_CACHE_REQUEST_MESSAGE_TYPE = 176;
		public const byte DROPPED_ITEMS_SYNC_MESSAGE_TYPE = 177;

		public static GulliverMod Instance { get; private set; }
		public static bool IsEnabled { get; private set; }

		private static readonly FieldInfo aiSpawnerPrefab = typeof( AIs.AISpawner ).GetField( "m_Prefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		private static readonly FieldInfo aiAnimationSpeed = typeof( AIs.AnimationModule ).GetField( "gulliverSpeedMultiplier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		private static readonly FieldInfo aiHumanAnimationSpeed = typeof( AIs.HumanAnimationModule ).GetField( "gulliverHumanSpeedMultiplier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );

		private readonly RaycastHit[] raycastHits = new RaycastHit[4];

		private readonly List<EntityData> scaledEntityCache = new List<EntityData>( MAX_NUMBER_OF_SCALED_ENTITIES_TO_CACHE );
		private readonly List<EntityData> modifiedEntities = new List<EntityData>( 4 );

		public readonly Dictionary<AIs.AISpawner, float> AISpawnerEntityScales = new Dictionary<AIs.AISpawner, float>( 32 );

		private float enabledStateNextSyncTime;
		private float modifiedEntitiesNextSyncTime;

		private bool uiVisible = false;
		private Rect windowRect;

		private KeyCode[] toggleKey;
		private float toggleKeyHoldDuration = 0.5f;
		private float toggleKeyHeldTime = 0f;
		private bool toggleKeyTriggered = false;
		private bool rmbHeld = false;

		private bool canUseHotkeysWhenHidden = false;

		private bool isPickingEntity;
		private Transform pickedEntity, hoveredEntity;
		private float pickedEntityMinScale, pickedEntityMaxScale;

		private void Awake()
		{
			if( !Instance )
				Instance = this;
			else if( this != Instance )
				Destroy( gameObject );
		}

		private void Start()
		{
			windowRect = new Rect( ( Screen.width - 400f ) * 0.5f, ( Screen.height - 100f ) * 0.5f, 400f, 110f );
			toggleKey = GetConfigurableKey( "GulliverMod", "ToggleKey" );

			foreach( KeyCode key in GetConfigurableKey( "GulliverMod", "HotkeysAlwaysWork" ) )
			{
				if( key == KeyCode.Y )
					canUseHotkeysWhenHidden = true;
			}

			foreach( KeyCode key in GetConfigurableKey( "GulliverMod", "HotkeyHoldTime" ) )
			{
				switch( key )
				{
					case KeyCode.Keypad0:
					case KeyCode.Alpha0: toggleKeyHoldDuration = 0f; break;
					case KeyCode.Keypad1:
					case KeyCode.Alpha1: toggleKeyHoldDuration = 0.1f; break;
					case KeyCode.Keypad2:
					case KeyCode.Alpha2: toggleKeyHoldDuration = 0.2f; break;
					case KeyCode.Keypad3:
					case KeyCode.Alpha3: toggleKeyHoldDuration = 0.3f; break;
					case KeyCode.Keypad4:
					case KeyCode.Alpha4: toggleKeyHoldDuration = 0.4f; break;
					case KeyCode.Keypad5:
					case KeyCode.Alpha5: toggleKeyHoldDuration = 0.5f; break;
					case KeyCode.Keypad6:
					case KeyCode.Alpha6: toggleKeyHoldDuration = 0.6f; break;
					case KeyCode.Keypad7:
					case KeyCode.Alpha7: toggleKeyHoldDuration = 0.7f; break;
					case KeyCode.Keypad8:
					case KeyCode.Alpha8: toggleKeyHoldDuration = 0.8f; break;
					case KeyCode.Keypad9:
					case KeyCode.Alpha9: toggleKeyHoldDuration = 0.9f; break;
				}
			}

			enabledStateNextSyncTime = Time.realtimeSinceStartup + ENABLED_STATE_SYNC_INTERVAL;
			modifiedEntitiesNextSyncTime = Time.realtimeSinceStartup + MODIFIED_ENTITIES_SYNC_INTERVAL;

			P2PSession.Instance.RegisterHandler( ENABLED_STATE_SYNC_MESSAGE_TYPE, OnModEnabledStateChanged );
			P2PSession.Instance.RegisterHandler( PLAYER_SCALE_SYNC_MESSAGE_TYPE, OnPlayerScaleChanged );
			P2PSession.Instance.RegisterHandler( ENTITY_SCALE_SYNC_MESSAGE_TYPE, OnEntityScaleChanged );
			P2PSession.Instance.RegisterHandler( ENTITY_SCALE_CACHE_REQUEST_MESSAGE_TYPE, OnEntityScalesRequested );
			P2PSession.Instance.RegisterHandler( DROPPED_ITEMS_SYNC_MESSAGE_TYPE, OnPeerDroppedItems );

			StartCoroutine( AddPlayerScaleHandlerComponentCoroutine() );
		}

		private void OnDisable()
		{
			P2PSession.Instance.UnregisterHandler( ENABLED_STATE_SYNC_MESSAGE_TYPE, OnModEnabledStateChanged );
			P2PSession.Instance.UnregisterHandler( PLAYER_SCALE_SYNC_MESSAGE_TYPE, OnPlayerScaleChanged );
			P2PSession.Instance.UnregisterHandler( ENTITY_SCALE_SYNC_MESSAGE_TYPE, OnEntityScaleChanged );
			P2PSession.Instance.UnregisterHandler( ENTITY_SCALE_CACHE_REQUEST_MESSAGE_TYPE, OnEntityScalesRequested );
			P2PSession.Instance.UnregisterHandler( DROPPED_ITEMS_SYNC_MESSAGE_TYPE, OnPeerDroppedItems );

			OnAboutToLeaveServer();
		}

		// Make sure host deactivates the mod before leaving the server
		public void OnAboutToLeaveServer()
		{
			if( Player.Get() && Player.Get().transform )
				Player.Get().transform.localScale = Vector3.one;

			if( IsEnabled )
			{
				IsEnabled = false;
				SendEnabledStateToPeers();
			}
		}

		// Add a PlayerScaleHandler component to this Player's P2PPlayer object (the Player representation that is
		// synchronized on all peers). This component notifies peers about changes to Player's scale and adjusts the Player's
		// animation speed, CharacterController values and etc. when the Player's scale changes
		private IEnumerator AddPlayerScaleHandlerComponentCoroutine()
		{
			while( !ReplicatedLogicalPlayer.s_LocalLogicalPlayer )
				yield return null;

			if( !ReplicatedLogicalPlayer.s_LocalLogicalPlayer.GetComponent<PlayerScaleHandler>() )
				ReplicatedLogicalPlayer.s_LocalLogicalPlayer.gameObject.AddComponent<PlayerScaleHandler>();

			yield return new WaitForSecondsRealtime( 1.5f );

			// Request scale values of entities from host
			if( !ReplTools.AmIMaster() )
			{
				P2PNetworkWriter writer = new P2PNetworkWriter();
				writer.StartMessage( ENTITY_SCALE_CACHE_REQUEST_MESSAGE_TYPE );
				writer.FinishMessage();
				P2PSession.Instance.SendWriterTo( P2PSession.Instance.GetSessionMaster( false ), writer, 0 ); // 0: TCP, 1: UDP
			}
		}

		private void Update()
		{
			float currentTime = Time.realtimeSinceStartup;

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
						if( toggleKeyHeldTime >= toggleKeyHoldDuration )
						{
							toggleKeyTriggered = false;
							SetUIVisible( !uiVisible );
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

			if( isPickingEntity )
			{
				hoveredEntity = null;

				// Raycast doesn't hit some animals easily, SphereCast is better
				//RaycastHit hit;
				//if( Physics.Raycast( CameraManager.Get().m_MainCamera.ScreenPointToRay( Input.mousePosition ), out hit, 100f, aiLayerMask ) )

				int hitCount = Physics.SphereCastNonAlloc( CameraManager.Get().m_MainCamera.ScreenPointToRay( Input.mousePosition ), 0.25f, raycastHits, 100f, AI_LAYER_MASK );
				for( int i = 0; i < hitCount && !hoveredEntity; i++ )
				{
					AIs.AI hitAI = raycastHits[i].transform.GetComponentInParent<AIs.AI>();
					if( hitAI )
					{
						hoveredEntity = hitAI.transform;

						if( Input.GetMouseButtonDown( 0 ) )
						{
							isPickingEntity = false;
							pickedEntity = hoveredEntity;

							Collider aiCollider = hitAI.GetComponent<Collider>();
							if( aiCollider )
							{
								Vector3 aiBounds = aiCollider.bounds.size;
								float boundsAverage = ( aiBounds.x + aiBounds.y + aiBounds.z ) / ( 3f * hoveredEntity.localScale.x );

								pickedEntityMinScale = Mathf.Min( MIN_ENTITY_SCALE / boundsAverage, hoveredEntity.localScale.x );
								pickedEntityMaxScale = MAX_ENTITY_SCALE / boundsAverage;
							}
							else
							{
								float defaultScale = GetDefaultScale( pickedEntity );
								pickedEntityMinScale = Mathf.Max( 0.1f, defaultScale * 0.1f );
								pickedEntityMaxScale = Mathf.Max( 10f, defaultScale * 10f );
							}
						}
					}
				}
			}

			// Regularly sync IsEnabled value with peers so that new users can fetch its value
			if( currentTime >= enabledStateNextSyncTime )
				SendEnabledStateToPeers();

			// Sync changes made to entities
			if( currentTime >= modifiedEntitiesNextSyncTime )
			{
				modifiedEntitiesNextSyncTime = currentTime + MODIFIED_ENTITIES_SYNC_INTERVAL;

				P2PNetworkWriter writer = CreateNetworkWriterForChangedEntities( modifiedEntities );
				if( writer != null )
				{
					P2PSession.Instance.SendWriterToAll( writer, 0 ); // 0: TCP, 1: UDP
					modifiedEntities.Clear();
				}
			}

			// Allow scaling player or pickedEntity (must hold Alt) with numpad - and + keys (click * key to reset scale)
			if( IsEnabled && ( uiVisible || canUseHotkeysWhenHidden ) )
			{
				if( Input.GetKey( KeyCode.KeypadPlus ) )
				{
					if( !Input.GetKey( KeyCode.LeftAlt ) )
						SetTargetScale( Player.Get().transform, Player.Get().transform.localScale.x * ( 1f + KEYBOARD_SCALE_SPEED * Time.deltaTime ) );
					else if( pickedEntity )
						SetTargetScale( pickedEntity, pickedEntity.localScale.x * ( 1f + KEYBOARD_SCALE_SPEED * Time.deltaTime ) );
				}
				else if( Input.GetKey( KeyCode.KeypadMinus ) )
				{
					if( !Input.GetKey( KeyCode.LeftAlt ) )
						SetTargetScale( Player.Get().transform, Player.Get().transform.localScale.x * Mathf.Max( 0.1f, 1f - KEYBOARD_SCALE_SPEED * Time.deltaTime ) );
					else if( pickedEntity )
						SetTargetScale( pickedEntity, pickedEntity.localScale.x * Mathf.Max( 0.1f, 1f - KEYBOARD_SCALE_SPEED * Time.deltaTime ) );
				}
				else if( Input.GetKeyDown( KeyCode.KeypadMultiply ) )
				{
					if( !Input.GetKey( KeyCode.LeftAlt ) )
						ResetTargetScale( Player.Get().transform );
					else if( pickedEntity )
						ResetTargetScale( pickedEntity );
				}
			}
		}

		private void OnGUI()
		{
			if( !uiVisible )
				return;

			GUI.skin = ModAPI.Interface.Skin;

			windowRect = GUILayout.Window( WINDOW_ID, windowRect, WindowOnGUI, "- Gulliver Mod -" );

			// Allow closing the menu with ESC key
			if( Event.current.isKey && Event.current.keyCode == KeyCode.Escape && !InputsManager.Get().m_TextInputActive )
			{
				SetUIVisible( false );
				GUI.FocusControl( null ); // Release keyboard focus from TextFields
			}

			// While interacting with the menu or scrolling through it, don't send input to game (i.e. don't fire an arrow or switch between weapons)
			if( windowRect.Contains( Event.current.mousePosition ) && ( !Mathf.Approximately( Input.mouseScrollDelta.y, 0f ) || Input.GetMouseButton( 0 ) ) )
				Input.ResetInputAxes();

			// While picking entities, show a tooltip on top of the hovered entity
			if( isPickingEntity && hoveredEntity && Event.current.type == EventType.Repaint )
			{
				Vector2 tooltipSize = GUI.skin.box.CalcSize( new GUIContent( hoveredEntity.name ) );
				Vector2 mousePos = Event.current.mousePosition;
				GUI.Box( new Rect( mousePos.x - tooltipSize.x * 0.5f, mousePos.y - tooltipSize.y, tooltipSize.x, tooltipSize.y ), hoveredEntity.name );
			}
		}

		private void WindowOnGUI( int id )
		{
			bool isHost = ReplTools.AmIMaster();

			GUILayout.BeginVertical( GUI.skin.box );

			GUILayout.BeginHorizontal();

			// Only the host can activate the mod
			if( isHost )
			{
				bool modEnabled = GUILayout.Toggle( IsEnabled, "MOD ENABLED" );
				if( modEnabled != IsEnabled )
				{
					// If mod is disabled, reset scaled entities' scales
					if( !modEnabled )
					{
						for( int i = 0; i < scaledEntityCache.Count; i++ )
						{
							if( scaledEntityCache[i].transform )
							{
								ResetTargetScale( scaledEntityCache[i].transform );

								if( scaledEntityCache[i].animator )
									scaledEntityCache[i].SetSpeedMultiplier( 1f );
							}
						}
					}

					OnModEnabledStateChanged( modEnabled );

					// Broadcast the change immediately
					SendEnabledStateToPeers();
				}
			}
			else if( !IsEnabled )
				GUILayout.Box( "Host hasn't activated the mod." );
			else
			{
				GUI.enabled = false;
				GUILayout.Toggle( true, "MOD ENABLED" );
				GUI.enabled = true;
			}

			GUILayout.FlexibleSpace();

			if( GUILayout.Button( "X" ) )
			{
				SetUIVisible( false );
				GUIUtility.ExitGUI();
			}

			GUILayout.EndHorizontal();

			// Show sliders to scale the player and the picked entity (if any)
			GUI.enabled = IsEnabled;
			ShowScaleSlider( Player.Get().transform, MIN_PLAYER_SCALE, MAX_PLAYER_SCALE );
			GUILayout.Space( 5f );
			ShowScaleSlider( pickedEntity, pickedEntityMinScale, pickedEntityMaxScale );
			GUI.enabled = true;

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		private void ShowScaleSlider( Transform target, float minScale, float maxScale )
		{
			GUILayout.Label( target ? ( target.name + ": " ) : "Entity: " );

			GUILayout.BeginHorizontal();

			if( target )
			{
				float currentScale = target.localScale.x;

				// Use log10 numbers in the slider or larger scales take the majority of the space in the slider
				GUI.changed = false;
				float scale = Mathf.Pow( 10f, GUILayout.HorizontalSlider( Mathf.Log10( currentScale ), Mathf.Log10( minScale ), Mathf.Log10( maxScale ), GUILayout.Width( 225f ) ) );
				if( !GUI.changed ) // There can be floating point imprecision issues with Pow and Log10, don't change scale user hasn't interacted with the slider
					scale = currentScale;

				GUILayout.Label( " " + scale.ToString( "F2" ), GUILayout.ExpandWidth( false ) );

				if( Mathf.Abs( currentScale - scale ) >= 0.01f )
					SetTargetScale( target, scale );

				GUILayout.Space( 10f );

				if( GUILayout.Button( "Reset" ) )
					ResetTargetScale( target );
			}

			// Show Pick button to pick an entity
			if( !target || target != Player.Get().transform )
				isPickingEntity = GUILayout.Toggle( isPickingEntity, isPickingEntity ? "Picking" : "Pick", GUI.skin.button );

			GUILayout.EndHorizontal();
		}

		// Host has activated or deactivated the mod
		private void OnModEnabledStateChanged( P2PNetworkMessage message )
		{
			bool modEnabled = message.m_Reader.ReadBoolean();
			if( !ReplTools.AmIMaster() && ( message.m_Connection.m_Peer == null || message.m_Connection.m_Peer.IsMaster() ) )
				OnModEnabledStateChanged( modEnabled );
		}

		private void OnModEnabledStateChanged( bool modEnabled )
		{
			if( IsEnabled != modEnabled )
			{
				IsEnabled = modEnabled;
				if( !IsEnabled )
				{
					SetPlayerScale( 1f );

					scaledEntityCache.Clear();
					AISpawnerEntityScales.Clear();
				}

				HUDTextChatHistory.AddMessageLocalized( modEnabled ? "Activated Gulliver Mod" : "Deactivated Gulliver Mod", Color.cyan );
			}

			// Camera can sometimes glitch out during the game, fix the glitch when mod is toggled
			if( CameraManager.Get() && CameraManager.Get().m_MainCamera )
			{
				CameraManager.Get().m_MainCamera.enabled = false;
				CameraManager.Get().m_MainCamera.enabled = true;
			}
		}

		// Synchronize IsEnabled's value with peers
		private void SendEnabledStateToPeers()
		{
			enabledStateNextSyncTime = Time.realtimeSinceStartup + ENABLED_STATE_SYNC_INTERVAL;

			// Master (host) determines whether or not the mod is enabled
			if( ReplTools.AmIMaster() && !ReplTools.IsPlayingAlone() )
			{
				P2PNetworkWriter writer = new P2PNetworkWriter();
				writer.StartMessage( ENABLED_STATE_SYNC_MESSAGE_TYPE );
				writer.Write( IsEnabled );
				writer.FinishMessage();
				P2PSession.Instance.SendWriterToAll( writer, 0 ); // 0: TCP, 1: UDP
			}
		}

		// A player's scale has changed
		private void OnPlayerScaleChanged( P2PNetworkMessage message )
		{
			// Don't receive the scale changes that we've transmitted ourselves
			if( message.m_Connection.m_Peer.IsLocalPeer() )
				return;

			GameObject obj = message.m_Reader.ReadGameObject();
			float scale = message.m_Reader.ReadFloat();
			if( obj )
				obj.transform.localScale = new Vector3( scale, scale, scale );
		}

		// One or more entities' scale have changed
		private void OnEntityScaleChanged( P2PNetworkMessage message )
		{
			// Don't receive the scale changes that we've transmitted ourselves
			if( message.m_Connection.m_Peer.IsLocalPeer() )
				return;

			// If we want to change scaled entities' animation speeds on all clients, then all clients must
			// keep track of the scaled entities (i.e. isHost check is commented out)
			//bool isHost = ReplTools.AmIMaster();
			int count = message.m_Reader.ReadByte();
			for( int i = 0; i < count; i++ )
			{
				GameObject obj = message.m_Reader.ReadGameObject();
				float scale = message.m_Reader.ReadFloat();
				if( obj )
				{
					obj.transform.localScale = new Vector3( scale, scale, scale );

					//if( isHost )
					AddScaledEntityToCache( obj.transform, true );
				}
			}
		}

		// An AISpawner spawned an entity with custom scale
		public void OnScaledEntitySpawned( Transform entity )
		{
			EntityData entityData = AddScaledEntityToCache( entity, false );

			int modifiedEntityIndex = IndexOfEntityInList( modifiedEntities, entity );
			if( modifiedEntityIndex < 0 )
				modifiedEntities.Add( entityData );
		}

		// A new player has joined the server and requested the scale values of entities from the host
		private void OnEntityScalesRequested( P2PNetworkMessage message )
		{
			if( message.m_Connection.m_Peer.IsLocalPeer() )
			{
				ModAPI.Log.Write( "OnEntityScalesRequested sent to self, this shouldn't happen!" );
				return;
			}

			if( message.m_Connection.m_IsReady )
			{
				P2PNetworkWriter writer = CreateNetworkWriterForChangedEntities( scaledEntityCache );
				if( writer != null )
					message.m_Connection.SendWriter( writer, 0 ); // 0: TCP, 1: UDP
			}
		}

		// Dropped items don't reset their scale automatically on peers, so manually notify others about these items
		public void OnPlayerDroppedItems( GameObject[] droppedItems )
		{
			StartCoroutine( SendDroppedItemsToPeersCoroutine( droppedItems ) );
		}

		private IEnumerator SendDroppedItemsToPeersCoroutine( GameObject[] droppedItems )
		{
			yield return new WaitForSecondsRealtime( 0.2f );

			if( droppedItems != null && droppedItems.Length > 0 )
			{
				P2PNetworkWriter writer = new P2PNetworkWriter();
				writer.StartMessage( DROPPED_ITEMS_SYNC_MESSAGE_TYPE );
				writer.Write( (byte) droppedItems.Length );
				for( int i = 0; i < droppedItems.Length; i++ )
					writer.Write( droppedItems[i].gameObject );
				writer.FinishMessage();
				P2PSession.Instance.SendWriterToAll( writer, 0 ); // 0: TCP, 1: UDP
			}
		}

		// Another player has dropped some items, reset these items' scale values
		private void OnPeerDroppedItems( P2PNetworkMessage message )
		{
			// Ignore the items that we've dropped ourselves
			if( message.m_Connection.m_Peer.IsLocalPeer() )
				return;

			// Fetch the dropped items
			int count = message.m_Reader.ReadByte();
			List<Item> droppedItems = new List<Item>( count );
			for( int i = 0; i < count; i++ )
			{
				GameObject obj = message.m_Reader.ReadGameObject();
				if( obj )
					droppedItems.Add( obj.GetComponent<Item>() );
			}

			StartCoroutine( ScalePeersDroppedItemsCoroutine( droppedItems ) );
		}

		private IEnumerator ScalePeersDroppedItemsCoroutine( List<Item> droppedItems )
		{
			// We'll loop "scaleAttempts" times and reset the scale values of items with no parent object (i.e. not held by a player)
			// We're iterating several times because due to network latency, dropped items might not register immediately on all clients
			int scaleAttempts = 0;
			while( droppedItems.Count > 0 && scaleAttempts < 5 )
			{
				for( int i = droppedItems.Count - 1; i >= 0; i-- )
				{
					if( !droppedItems[i] )
						droppedItems.RemoveAt( i );
					else if( !droppedItems[i].transform.parent )
					{
						droppedItems[i].ResetScale();
						droppedItems.RemoveAt( i );
					}
				}

				scaleAttempts++;
				yield return new WaitForSecondsRealtime( 0.25f );
			}
		}

		// Calculate an entity's default scale
		private static float GetDefaultScale( Transform target )
		{
			if( !target || target == Player.Get().transform )
				return 1f;

			AIs.AI ai = target.GetComponent<AIs.AI>();
			if( ai && ai.m_Spawner )
			{
				float aiAverageScale = ( ai.m_Spawner.m_MinScale + ai.m_Spawner.m_MaxScale ) * 0.5f;
				if( aiSpawnerPrefab != null )
				{
					GameObject aiPrefab = aiSpawnerPrefab.GetValue( ai.m_Spawner ) as GameObject;
					if( aiPrefab )
						aiAverageScale *= aiPrefab.transform.localScale.x;
				}

				return aiAverageScale;
			}

			return 1f;
		}

		private void ResetTargetScale( Transform target )
		{
			SetTargetScale( target, GetDefaultScale( target ), false );
		}

		private void SetTargetScale( Transform target, float scale, bool clamp = true )
		{
			if( !target )
				return;

			if( target == Player.Get().transform )
				SetPlayerScale( scale, clamp );
			else
				SetEntityScale( target, scale, clamp );
		}

		public void SetPlayerScale( float scale, bool clamp = true )
		{
			if( !IsEnabled && !Mathf.Approximately( scale, 1f ) )
			{
				ModAPI.Log.Write( "Can't change player's scale when Gulliver Mod is inactive." );
				return;
			}

			if( clamp )
				scale = Mathf.Clamp( scale, MIN_PLAYER_SCALE, MAX_PLAYER_SCALE );

			Player.Get().transform.localScale = new Vector3( scale, scale, scale );

			// Camera can sometimes glitch out after shrinking, fix the glitch when scale is reset
			if( Mathf.Approximately( scale, 1f ) )
			{
				CameraManager.Get().m_MainCamera.enabled = false;
				CameraManager.Get().m_MainCamera.enabled = true;
			}
		}

		private void SetEntityScale( Transform entity, float scale, bool clamp = true )
		{
			if( clamp )
				scale = Mathf.Clamp( scale, pickedEntityMinScale, pickedEntityMaxScale );

			entity.localScale = new Vector3( scale, scale, scale );

			// If we want to change scaled entities' animation speeds on all clients, then all clients must
			// keep track of the scaled entities (i.e. ReplTools.AmIMaster() check is commented out)
			//if( ReplTools.AmIMaster() )
			//AddScaledEntityToCache( entity );

			EntityData entityData = AddScaledEntityToCache( entity, true );

			int modifiedEntityIndex = IndexOfEntityInList( modifiedEntities, entity );
			if( modifiedEntityIndex < 0 )
				modifiedEntities.Add( entityData );
		}

		private EntityData AddScaledEntityToCache( Transform scaledEntity, bool updateSpawnerScale )
		{
			EntityData result;
			int entityIndex = IndexOfEntityInList( scaledEntityCache, scaledEntity );
			if( entityIndex >= 0 )
				result = scaledEntityCache[entityIndex];
			else
			{
				for( int i = scaledEntityCache.Count - 1; i >= 0; i-- )
				{
					if( !scaledEntityCache[i].transform )
						scaledEntityCache.RemoveAt( i );
				}

				if( scaledEntityCache.Count >= MAX_NUMBER_OF_SCALED_ENTITIES_TO_CACHE )
					scaledEntityCache.RemoveAt( 0 );

				result = new EntityData( scaledEntity );
				scaledEntityCache.Add( result );
			}

			// Recalculate animator speed
			if( result.animator )
				result.SetSpeedMultiplier( Mathf.Clamp( 1f / GetMultipliedScale( result.transform.localScale.x * result.invDefaultScale, 0.25f ), 0.25f, 1f ) );

			// If this entity isn't just spawned from an AISpawner, set the custom scale value of that AISpawner to this entity's scale
			if( updateSpawnerScale )
			{
				AIs.AI ai = scaledEntity.GetComponent<AIs.AI>();
				if( ai && ai.m_Spawner )
					AISpawnerEntityScales[ai.m_Spawner] = scaledEntity.transform.localScale.x;
			}

			return result;
		}

		// Create a P2PNetworkWriter from a list of entities. This P2PNetworkWriter can then be sent to peers
		private P2PNetworkWriter CreateNetworkWriterForChangedEntities( List<EntityData> changedEntities )
		{
			for( int i = changedEntities.Count - 1; i >= 0; i-- )
			{
				if( !changedEntities[i].transform )
					changedEntities.RemoveAt( i );
			}

			if( changedEntities.Count == 0 )
				return null;

			P2PNetworkWriter writer = new P2PNetworkWriter();
			writer.StartMessage( ENTITY_SCALE_SYNC_MESSAGE_TYPE );
			writer.Write( (byte) changedEntities.Count );
			for( int i = 0; i < changedEntities.Count; i++ )
			{
				writer.Write( changedEntities[i].transform.gameObject );
				writer.Write( changedEntities[i].transform.localScale.x );
			}
			writer.FinishMessage();

			return writer;
		}

		private int IndexOfEntityInList( List<EntityData> entities, Transform entity )
		{
			for( int i = entities.Count - 1; i >= 0; i-- )
			{
				if( entities[i].transform == entity )
					return i;
			}

			return -1;
		}

		// Default scale is considered 1.0. This function multiplies additional scale value by multiplier;
		// i.e. 1.5 scale has 0.5 additional scale. When multiplier is set to 0.25, additional scale becomes
		// 0.5 x 0.25 = 0.125 and the resulting scale becomes 1.0 + 0.125 = 1.125
		public static float GetMultipliedScale( float scale, float multiplier )
		{
			return 1f + ( scale - 1f ) * multiplier;
		}

		// Show/hide the mod's menu
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
				isPickingEntity = false;

				if( rmbHeld )
					rmbHeld = false;
				else
					player.UnblockRotation();

				player.UnblockInspection();
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
								else if( keyRawSplit[i].Length == 2 && keyRawSplit[i][0] == 'D' )
								{
									string potentialDigitKey = "Alpha" + keyRawSplit[i][1];
									if( System.Enum.IsDefined( typeof( KeyCode ), potentialDigitKey ) )
										keys.Add( (KeyCode) System.Enum.Parse( typeof( KeyCode ), potentialDigitKey ) );
								}
							}
						}
					}
				}
			}

			return keys.ToArray();
		}
	}
}