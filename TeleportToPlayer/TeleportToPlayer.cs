using System.Globalization;

namespace GreenHell_TeleportToPlayer
{
	public class HUDTextChatExtended : HUDTextChat
	{
		protected override void SendTextMessage()
		{
			string text = m_Field.text.Trim();
			if( !text.StartsWith( "/tp" ) )
				base.SendTextMessage();
			else
			{
				CompareInfo caseInsensitiveComparer = new CultureInfo( "en-US" ).CompareInfo;

				string playerName = text.Substring( 3 ).Trim();
				foreach( ReplicatedLogicalPlayer player in ReplicatedLogicalPlayer.s_AllLogicalPlayers )
				{
					if( caseInsensitiveComparer.Compare( playerName, player.GetP2PPeer().GetDisplayName(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					{
						Player.Get().Teleport( player.gameObject, false );
						break;
					}
				}
			}
		}
	}
}