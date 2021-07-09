// Contains all the network messages that we need.
using Mirror;

// client to server ////////////////////////////////////////////////////////////
public partial class LoginMsg : MessageBase {
    public string player;
    public string version;
}

public partial class ChangeTeamMsg : MessageBase {
    public Team team;
}

public partial class ChangeHeroMsg : MessageBase {
    public int heroIndex;
}

// 'SetReady' message would be too confusing since UNET uses one too
public partial class LockMsg : MessageBase {
}

// server to client ////////////////////////////////////////////////////////////
// we need an error msg packet because we can't use TargetRpc with the Network-
// Manager, since it's not a MonoBehaviour.
public partial class ErrorMsg : MessageBase {
    public string text;
    public bool causesDisconnect;
}

public partial class LobbyUpdateMsg : MessageBase {
    public LobbyPlayer[] players;
}