using Mirror;
using UnityEngine;

public class PlayerRewindInput : NetworkBehaviour
{
    [SerializeField] private KeyCode rewindKey = KeyCode.Z;

    private PlayerRewind rewind;

    private void Awake()
    {
        rewind = GetComponent<PlayerRewind>();
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        if (rewind == null)
            return;

        if (rewind.IsRewinding)
            return;

        if (Input.GetKeyDown(rewindKey))
            rewind.CmdRequestRewind();
    }
}