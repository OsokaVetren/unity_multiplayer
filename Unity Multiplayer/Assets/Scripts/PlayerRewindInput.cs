using Mirror;
using UnityEngine;

public class PlayerRewindInput : NetworkBehaviour
{
    [SerializeField] private KeyCode rewindKey = KeyCode.Q;

    private PlayerRewind rewind;

    private void Awake()
    {
        rewind = GetComponent<PlayerRewind>();
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        if (Input.GetKeyDown(rewindKey) && rewind != null)
            rewind.CmdRequestRewind();
    }
}